using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace ValheimCompanion
{
    internal static class CompanionController
    {
        private static readonly string[] WolfPrefabNames = { "Wolf", "Wolf_Tamed", "Lucy" };
        private const string CompanionMarkerKey = "ValheimCompanion";
        private const string CompanionOwnerKey = "ValheimCompanionOwner";
        private const string CompanionMarker = Plugin.PluginGuid;
        private const float SpawnSearchRadius = 8f;
        private const float DefenseRadius = 24f;
        private const float GatherRadius = 6f;
        private const int SpawnSearchFrames = 120;
        private static readonly float GatherRadiusSquared = GatherRadius * GatherRadius;
        private static readonly FieldInfo? TargetCreatureField =
            typeof(MonsterAI).GetField("m_targetCreature", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? LastKnownTargetPositionField =
            typeof(MonsterAI).GetField("m_lastKnownTargetPos", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? BeenAtLastPositionField =
            typeof(MonsterAI).GetField("m_beenAtLastPos", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? StaticTargetField =
            typeof(MonsterAI).GetField("m_targetStatic", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? SetAlertedMethod =
            typeof(MonsterAI).GetMethod("SetAlerted", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
        private static bool _spawnOutcomeUnverified;
        private static MonsterAI? _engagedCompanionAI;
        private static Character? _engagedTarget;
        private static bool _reportedMissingCompanionForFight;
        private static bool _reportedTargetMethodUnavailable;
        private static bool _reportedCombatTargetFailure;
        private static bool _reportedCompanionOwnershipFailure;
        private static bool _reportedMissingCompanionForGather;
        private static int _gatheredItemsSinceReport;
        private static float _nextGatherReportTime;
        private static Character? _targetRestrictionCharacter;
        private static bool _originalCannotTargetOthers;
        private static ManualLogSource? _combatLogger;
        private static bool _reportedTargetRestoration;

        public static IEnumerator Summon(ManualLogSource logger)
        {
            if (_spawnOutcomeUnverified)
            {
                logger.LogError("A previous summon request could not be verified. Reload the test world before trying again to avoid creating duplicate wolves.");
                yield break;
            }

            var player = Player.m_localPlayer;
            if (player == null)
            {
                logger.LogWarning("Summon is only available after entering a world.");
                yield break;
            }

            var network = ZNet.instance;
            if (network == null || !network.IsServer() || network.IsDedicated())
            {
                logger.LogWarning("Summoning is host-only. In multiplayer, use this hotkey from the player's Steam-hosted world.");
                yield break;
            }

            var scene = ZNetScene.instance;
            if (scene == null)
            {
                logger.LogError("Cannot summon: Valheim's network scene is not ready.");
                yield break;
            }

            var ownerId = player.GetPlayerID();
            var existing = FindOwnedCompanion(ownerId);
            if (existing != null)
            {
                FollowPlayer(existing, player, logger);
                yield break;
            }

            var prefab = FindWolfPrefab(scene, out var prefabName);
            if (prefab == null)
            {
                logger.LogError(
                    $"Cannot summon: no wolf prefab was found. Tried: {string.Join(", ", WolfPrefabNames)}.");
                yield break;
            }

            if (prefab.GetComponent<Tameable>() == null
                || prefab.GetComponent<Character>() == null
                || prefab.GetComponent<MonsterAI>() == null
                || prefab.GetComponent<ZNetView>() == null)
            {
                logger.LogError(
                    $"Cannot summon: prefab '{prefabName}' is missing required tameable/network components.");
                yield break;
            }
            if (prefabName != "Wolf")
            {
                logger.LogWarning($"Using alternate wolf prefab '{prefabName}' because the canonical 'Wolf' prefab was unavailable.");
            }

            var existingZdos = CaptureLoadedZdos();
            var spawnPosition = player.transform.position
                + player.transform.forward * 2.5f
                + player.transform.right * 1.5f;
            _spawnOutcomeUnverified = true;
            scene.SpawnObject(spawnPosition, Quaternion.LookRotation(player.transform.forward), prefab);

            var prefabHash = scene.GetPrefabHash(prefab);
            for (var frame = 0; frame < SpawnSearchFrames; frame++)
            {
                yield return null;

                var spawned = FindNewWolf(existingZdos, prefabHash, spawnPosition);
                if (spawned == null)
                {
                    continue;
                }

                if (!PrepareCompanion(spawned, player, ownerId, logger))
                {
                    scene.Destroy(spawned.gameObject);
                    _spawnOutcomeUnverified = false;
                    yield break;
                }

                _spawnOutcomeUnverified = false;
                logger.LogInfo($"Summoned Lucy using the '{prefabName}' wolf prefab. She is saved in this world; use the dismiss hotkey to remove her.");
                yield break;
            }

            logger.LogError("Valheim did not expose the newly spawned wolf to the local scene in time. No companion was configured; check the game log before retrying.");
        }

        private static GameObject? FindWolfPrefab(ZNetScene scene, out string prefabName)
        {
            foreach (var candidateName in WolfPrefabNames)
            {
                var prefab = scene.GetPrefab(candidateName);
                if (prefab != null)
                {
                    prefabName = candidateName;
                    return prefab;
                }
            }

            prefabName = string.Empty;
            return null;
        }

        public static void Dismiss(ManualLogSource logger)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                logger.LogWarning("Dismiss is only available after entering a world.");
                return;
            }

            var network = ZNet.instance;
            if (network == null || !network.IsServer() || network.IsDedicated())
            {
                logger.LogWarning("Dismiss is host-only. Use this hotkey from the player's Steam-hosted world.");
                return;
            }

            var scene = ZNetScene.instance;
            if (scene == null)
            {
                logger.LogError("Cannot dismiss: Valheim's network scene is not ready.");
                return;
            }

            var companion = FindOwnedCompanion(player.GetPlayerID());
            if (companion == null)
            {
                logger.LogInfo("No summoned companion was found in this world.");
                return;
            }

            var networkView = companion.GetComponent<ZNetView>();
            if (networkView == null || !networkView.IsValid() || !networkView.IsOwner())
            {
                logger.LogWarning("Cannot dismiss the companion because this host does not own its network object.");
                return;
            }

            scene.Destroy(companion.gameObject);
            logger.LogInfo("Dismissed the summoned companion from this world.");
        }

        public static void LogStatus(CompanionMode mode, ManualLogSource logger)
        {
            var player = Player.m_localPlayer;
            var companion = player == null ? null : FindOwnedCompanion(player.GetPlayerID());
            var companionStatus = companion == null ? "not summoned" : "summoned";
            var target = companion?.GetComponent<MonsterAI>()?.GetTargetCreature();
            var targetStatus = target == null
                ? "none"
                : $"{target.name} at {(target.transform.position - companion!.transform.position).magnitude:F1}m";
            var defendedTarget = _engagedCompanionAI == companion?.GetComponent<MonsterAI>()
                ? _engagedTarget?.name ?? "none"
                : "none";
            logger.LogInfo(
                $"Current companion mode: {mode}; companion: {companionStatus}; " +
                $"AI target: {targetStatus}; defended target: {defendedTarget}.");
        }

        public static void PreserveCombatTarget(MonsterAI companionAI)
        {
            if (companionAI != _engagedCompanionAI || _engagedTarget == null || _combatLogger == null)
            {
                return;
            }

            var companion = companionAI.GetComponent<Character>();
            var owner = companion == null ? null : FindCompanionOwner(companion);
            if (companion == null || owner == null || !IsValidDefenseTarget(_engagedTarget, companion, owner)
                || companionAI.GetTargetCreature() == _engagedTarget)
            {
                return;
            }

            if (TrySetCombatTarget(companionAI, _engagedTarget, _combatLogger) && !_reportedTargetRestoration)
            {
                _combatLogger.LogWarning(
                    "Valheim cleared Lucy's combat target during an AI update; restoring it so she can attack the selected threat.");
                _reportedTargetRestoration = true;
            }
        }

        public static void UpdateAppearances(ManualLogSource logger)
        {
            var network = ZNet.instance;
            if (network == null || network.IsDedicated())
            {
                return;
            }

            foreach (var tameable in UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None))
            {
                var networkView = tameable.GetComponent<ZNetView>();
                if (networkView == null || !networkView.IsValid())
                {
                    continue;
                }

                var zdo = networkView.GetZDO();
                if (zdo != null && zdo.GetString(CompanionMarkerKey, string.Empty) == CompanionMarker)
                {
                    LucyAppearance.Apply(tameable, logger);
                }
            }
        }

        internal static bool IsTaggedCompanion(Character? character)
        {
            if (character == null)
            {
                return false;
            }

            var networkView = character.GetComponent<ZNetView>();
            if (networkView == null || !networkView.IsValid())
            {
                return false;
            }

            var zdo = networkView.GetZDO();
            return zdo != null && zdo.GetString(CompanionMarkerKey, string.Empty) == CompanionMarker;
        }

        private static bool IsThreateningOwnerOrCompanion(Character target, Character companion)
        {
            var targetAI = target.GetComponent<BaseAI>();
            var owner = FindCompanionOwner(companion);
            return targetAI != null
                && owner != null
                && IsValidDefenseTarget(target, companion, owner)
                && (targetAI.GetTargetCreature() == owner || targetAI.GetTargetCreature() == companion);
        }

        private static Player? FindCompanionOwner(Character companion)
        {
            var networkView = companion.GetComponent<ZNetView>();
            var zdo = networkView != null && networkView.IsValid() ? networkView.GetZDO() : null;
            if (zdo == null || zdo.GetString(CompanionMarkerKey, string.Empty) != CompanionMarker)
            {
                return null;
            }

            var ownerId = zdo.GetLong(CompanionOwnerKey, 0L);
            foreach (var player in UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (player != null && player.GetPlayerID() == ownerId)
                {
                    return player;
                }
            }

            return null;
        }

        private static bool IsValidDefenseTarget(Character target, Character companion, Player owner)
        {
            if (CompanionModeState.Current != CompanionMode.Fight || target == null || target.IsDead()
                || !IsTaggedCompanion(companion)
                || (target.transform.position - owner.transform.position).sqrMagnitude > DefenseRadius * DefenseRadius)
            {
                return false;
            }

            var companionAI = companion.GetComponent<BaseAI>();
            return companionAI != null && companionAI.IsEnemy(target);
        }

        internal static bool CanDamageThreat(Character target, Character companion)
        {
            if (CompanionModeState.Current != CompanionMode.Fight)
            {
                return false;
            }

            var owner = FindCompanionOwner(companion);
            if (owner == null || !IsValidDefenseTarget(target, companion, owner))
            {
                return false;
            }

            return IsThreateningOwnerOrCompanion(target, companion)
                || (_engagedTarget == target && _engagedCompanionAI == companion.GetComponent<MonsterAI>());
        }

        internal static void LogDamageDecision(Character target, bool allowed)
        {
            if (_combatLogger != null)
            {
                var targetAI = target.GetComponent<BaseAI>();
                _combatLogger.LogInfo(
                    $"Lucy bite against {target.name} {(allowed ? "allowed" : "blocked")}; " +
                    $"target's current AI target: {targetAI?.GetTargetCreature()?.name ?? "none"}; " +
                    $"active defended target: {(_engagedTarget != null ? _engagedTarget.name : "none")}.");
            }
        }

        internal static void LogDamageResult(
            Character target,
            float healthBefore,
            float healthAfter,
            float blunt,
            float slash,
            float pierce)
        {
            if (_combatLogger == null)
            {
                return;
            }

            var message =
                $"Lucy hit result for {target.name}: health {healthBefore:0.##}->{healthAfter:0.##}; " +
                $"physical damage blunt={blunt:0.##}, slash={slash:0.##}, pierce={pierce:0.##}.";
            if (healthAfter >= healthBefore)
            {
                _combatLogger.LogWarning(message + " Valheim did not reduce target health for this allowed hit.");
            }
            else
            {
                _combatLogger.LogInfo(message);
            }
        }

        public static void UpdateMode(CompanionMode mode, ManualLogSource logger)
        {
            _combatLogger = logger;
            if (mode != CompanionMode.Fight)
            {
                RestoreTargetRestriction();
            }

            if (mode == CompanionMode.Gather)
            {
                ClearEngagement(logger);
                _reportedMissingCompanionForFight = false;
                _reportedCompanionOwnershipFailure = false;
                UpdateGatherMode(logger);
                return;
            }

            if (mode != CompanionMode.Fight)
            {
                ClearEngagement(logger);
                _reportedMissingCompanionForFight = false;
                _reportedCompanionOwnershipFailure = false;
                _reportedMissingCompanionForGather = false;
                return;
            }

            _reportedMissingCompanionForGather = false;
            var player = Player.m_localPlayer;
            var network = ZNet.instance;
            if (player == null || network == null || !network.IsServer() || network.IsDedicated())
            {
                return;
            }

            var companion = FindOwnedCompanion(player.GetPlayerID());
            if (companion == null)
            {
                RestoreTargetRestriction();
                ClearEngagement(logger);
                if (!_reportedMissingCompanionForFight)
                {
                    logger.LogWarning("Fight mode is selected, but no companion is summoned. Press F10 in the test world to summon it.");
                    _reportedMissingCompanionForFight = true;
                }

                return;
            }

            _reportedMissingCompanionForFight = false;
            var networkView = companion.GetComponent<ZNetView>();
            var companionAI = companion.GetComponent<MonsterAI>();
            var companionCharacter = companion.GetComponent<Character>();
            if (networkView == null || !networkView.IsValid()
                || companionAI == null || companionCharacter == null || !companion.IsTamed())
            {
                RestoreTargetRestriction();
                ClearEngagement(logger);
                return;
            }

            if (!networkView.IsOwner())
            {
                networkView.ClaimOwnership();
            }

            if (!networkView.IsOwner())
            {
                RestoreTargetRestriction();
                ClearEngagement(logger);
                if (!_reportedCompanionOwnershipFailure)
                {
                    logger.LogWarning("Fight mode is host-only, but the host does not own the companion's network object.");
                    _reportedCompanionOwnershipFailure = true;
                }

                return;
            }

            _reportedCompanionOwnershipFailure = false;
            if (!CanSetCombatTarget())
            {
                if (!_reportedTargetMethodUnavailable)
                {
                    logger.LogError("Fight mode is unavailable: the expected Valheim MonsterAI target fields or alert method were not found.");
                    _reportedTargetMethodUnavailable = true;
                }
                return;
            }

            var target = FindThreat(player, companionCharacter);
            if (target == null)
            {
                ClearEngagement(logger);
                RestoreTargetRestriction();
                return;
            }

            AllowCompanionToTargetEnemies(companionCharacter, logger);

            if (_engagedCompanionAI == companionAI
                && _engagedTarget == target
                && companionAI.GetTargetCreature() == target)
            {
                return;
            }

            if (!TrySetCombatTarget(companionAI, target, logger))
            {
                return;
            }

            _engagedCompanionAI = companionAI;
            _engagedTarget = target;
            _reportedTargetRestoration = false;
            logger.LogInfo($"Your companion is defending you from {target.name}.");
        }

        private static void UpdateGatherMode(ManualLogSource logger)
        {
            var player = Player.m_localPlayer;
            var network = ZNet.instance;
            if (player == null || network == null || !network.IsServer() || network.IsDedicated())
            {
                return;
            }

            var companion = FindOwnedCompanion(player.GetPlayerID());
            if (companion == null)
            {
                if (!_reportedMissingCompanionForGather)
                {
                    logger.LogWarning("Gather mode is selected, but no companion is summoned. Press F10 in the test world to summon it.");
                    _reportedMissingCompanionForGather = true;
                }

                return;
            }

            _reportedMissingCompanionForGather = false;
            var networkView = companion.GetComponent<ZNetView>();
            if (networkView == null || !networkView.IsValid() || !companion.IsTamed())
            {
                return;
            }

            if (!networkView.IsOwner())
            {
                networkView.ClaimOwnership();
            }

            if (!networkView.IsOwner())
            {
                return;
            }

            var inventory = player.GetInventory();
            if (inventory == null)
            {
                return;
            }

            var playerPosition = player.transform.position;
            var nearestDistanceSquared = GatherRadiusSquared;
            Interactable? nearestPickup = null;

            foreach (var itemDrop in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (itemDrop == null || itemDrop.IsPiece()
                    || (itemDrop.transform.position - playerPosition).sqrMagnitude > nearestDistanceSquared
                    || !itemDrop.CanPickup(false))
                {
                    continue;
                }

                var interactable = itemDrop as Interactable;
                if (interactable == null)
                {
                    continue;
                }

                nearestDistanceSquared = (itemDrop.transform.position - playerPosition).sqrMagnitude;
                nearestPickup = interactable;
            }

            foreach (var pickable in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))
            {
                if (pickable == null || !pickable.CanBePicked()
                    || (pickable.transform.position - playerPosition).sqrMagnitude > nearestDistanceSquared)
                {
                    continue;
                }

                var pickableNetworkView = pickable.GetComponent<ZNetView>();
                if (pickableNetworkView == null || !pickableNetworkView.IsValid())
                {
                    continue;
                }

                if (!pickableNetworkView.IsOwner())
                {
                    pickableNetworkView.ClaimOwnership();
                }

                if (!pickableNetworkView.IsOwner())
                {
                    continue;
                }

                var interactable = pickable as Interactable;
                if (interactable == null)
                {
                    continue;
                }
                nearestDistanceSquared = (pickable.transform.position - playerPosition).sqrMagnitude;
                nearestPickup = interactable;
            }

            if (nearestPickup != null && nearestPickup.Interact(player, false, false))
            {
                _gatheredItemsSinceReport++;
                if (Time.unscaledTime >= _nextGatherReportTime)
                {
                    logger.LogInfo($"Gather mode collected {_gatheredItemsSinceReport} nearby item(s) into your inventory.");
                    _gatheredItemsSinceReport = 0;
                    _nextGatherReportTime = Time.unscaledTime + 5f;
                }

                return;
            }
        }

        private static Character? FindThreat(Player player, Character companion)
        {
            var companionAI = companion.GetComponent<BaseAI>();
            if (_engagedCompanionAI == companion.GetComponent<MonsterAI>()
                && _engagedTarget != null
                && IsValidDefenseTarget(_engagedTarget, companion, player))
            {
                return _engagedTarget;
            }

            var maxDistanceSquared = DefenseRadius * DefenseRadius;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None))
            {
                if (candidate == null || candidate == player || candidate.IsDead()
                    || (candidate.transform.position - player.transform.position).sqrMagnitude > maxDistanceSquared)
                {
                    continue;
                }

                if (companionAI != null && IsThreateningOwnerOrCompanion(candidate, companion))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ClearEngagement(ManualLogSource logger)
        {
            if (_engagedCompanionAI == null || _engagedTarget == null || !CanSetCombatTarget())
            {
                _engagedCompanionAI = null;
                _engagedTarget = null;
                return;
            }

            if (_engagedCompanionAI.GetTargetCreature() == _engagedTarget)
            {
                if (TrySetCombatTarget(_engagedCompanionAI, null, logger))
                {
                    logger.LogInfo("Your companion stopped defending and returned to following you.");
                }
            }

            _engagedCompanionAI = null;
            _engagedTarget = null;
        }

        private static void AllowCompanionToTargetEnemies(Character companion, ManualLogSource logger)
        {
            if (_targetRestrictionCharacter != companion)
            {
                RestoreTargetRestriction();
                _targetRestrictionCharacter = companion;
                _originalCannotTargetOthers = companion.m_aiCannotTargetOthers;
                logger.LogInfo(
                    $"Fight mode temporarily allows Lucy's wolf AI to target enemies " +
                    $"(original m_aiCannotTargetOthers={_originalCannotTargetOthers}).");
            }

            companion.m_aiCannotTargetOthers = false;
        }

        private static void RestoreTargetRestriction()
        {
            if (_targetRestrictionCharacter != null)
            {
                _targetRestrictionCharacter.m_aiCannotTargetOthers = _originalCannotTargetOthers;
                _targetRestrictionCharacter = null;
            }
        }

        private static bool TrySetCombatTarget(MonsterAI companionAI, Character? target, ManualLogSource logger)
        {
            if (!CanSetCombatTarget())
            {
                return false;
            }

            TargetCreatureField!.SetValue(companionAI, target);
            StaticTargetField!.SetValue(companionAI, null);
            if (target != null)
            {
                LastKnownTargetPositionField!.SetValue(companionAI, target.transform.position);
                BeenAtLastPositionField!.SetValue(companionAI, false);
            }

            try
            {
                SetAlertedMethod!.Invoke(companionAI, new object[] { target != null });
            }
            catch (TargetInvocationException exception)
            {
                if (!_reportedCombatTargetFailure)
                {
                    var cause = exception.InnerException ?? exception;
                    logger.LogError($"Valheim rejected Lucy's alert state ({cause.GetType().Name}): {cause.Message}");
                    _reportedCombatTargetFailure = true;
                }

                return false;
            }

            var applied = companionAI.GetTargetCreature() == target;
            if (!applied && !_reportedCombatTargetFailure)
            {
                logger.LogError(
                    $"Valheim did not apply Lucy's combat target: expected={target?.name ?? "none"}; " +
                    $"actual={companionAI.GetTargetCreature()?.name ?? "none"}.");
                _reportedCombatTargetFailure = true;
            }

            return applied;
        }

        private static bool CanSetCombatTarget()
        {
            return TargetCreatureField != null
                && LastKnownTargetPositionField != null
                && BeenAtLastPositionField != null
                && StaticTargetField != null
                && SetAlertedMethod != null;
        }

        private static HashSet<ZDO> CaptureLoadedZdos()
        {
            var zdos = new HashSet<ZDO>();
            foreach (var tameable in UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None))
            {
                var networkView = tameable.GetComponent<ZNetView>();
                if (networkView != null && networkView.IsValid())
                {
                    var zdo = networkView.GetZDO();
                    if (zdo != null)
                    {
                        zdos.Add(zdo);
                    }
                }
            }

            return zdos;
        }

        private static Tameable? FindOwnedCompanion(long ownerId)
        {
            foreach (var tameable in UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None))
            {
                var networkView = tameable.GetComponent<ZNetView>();
                if (networkView == null || !networkView.IsValid())
                {
                    continue;
                }

                var zdo = networkView.GetZDO();
                if (zdo != null
                    && zdo.GetString(CompanionMarkerKey, string.Empty) == CompanionMarker
                    && zdo.GetLong(CompanionOwnerKey, 0L) == ownerId)
                {
                    return tameable;
                }
            }

            return null;
        }

        private static Tameable? FindNewWolf(HashSet<ZDO> existingZdos, int prefabHash, Vector3 spawnPosition)
        {
            var maxDistanceSquared = SpawnSearchRadius * SpawnSearchRadius;
            foreach (var tameable in UnityEngine.Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None))
            {
                var networkView = tameable.GetComponent<ZNetView>();
                if (networkView == null || !networkView.IsValid())
                {
                    continue;
                }

                var zdo = networkView.GetZDO();
                if (zdo != null
                    && !existingZdos.Contains(zdo)
                    && zdo.GetPrefab() == prefabHash
                    && (tameable.transform.position - spawnPosition).sqrMagnitude <= maxDistanceSquared)
                {
                    return tameable;
                }
            }

            return null;
        }

        private static bool PrepareCompanion(Tameable tameable, Player player, long ownerId, ManualLogSource logger)
        {
            var networkView = tameable.GetComponent<ZNetView>();
            var character = tameable.GetComponent<Character>();
            var monsterAI = tameable.GetComponent<MonsterAI>();
            var zdo = networkView != null && networkView.IsValid() ? networkView.GetZDO() : null;

            if (networkView == null || !networkView.IsValid() || zdo == null || character == null || monsterAI == null)
            {
                logger.LogError("The spawned wolf is missing a required network, character, or AI component.");
                return false;
            }

            if (!networkView.IsOwner())
            {
                networkView.ClaimOwnership();
            }

            if (!networkView.IsOwner())
            {
                logger.LogError("The host could not take ownership of the spawned wolf; refusing to alter it.");
                return false;
            }

            zdo.Set(CompanionMarkerKey, CompanionMarker);
            zdo.Set(CompanionOwnerKey, ownerId);
            character.SetTamed(true);
            monsterAI.MakeTame();

            if (!tameable.IsTamed())
            {
                logger.LogError("Valheim did not mark the spawned wolf as tame; refusing to leave it as a wild creature.");
                return false;
            }

            monsterAI.SetFollowTarget(player.gameObject);
            return true;
        }

        private static void FollowPlayer(Tameable companion, Player player, ManualLogSource logger)
        {
            var networkView = companion.GetComponent<ZNetView>();
            var monsterAI = companion.GetComponent<MonsterAI>();
            if (!companion.IsTamed() || networkView == null || !networkView.IsValid() || monsterAI == null)
            {
                logger.LogError("The saved companion is not in a valid tameable state; it was not changed.");
                return;
            }

            if (!networkView.IsOwner())
            {
                networkView.ClaimOwnership();
            }

            if (!networkView.IsOwner())
            {
                logger.LogError("The host could not take ownership of the saved companion; it was not changed.");
                return;
            }

            monsterAI.SetFollowTarget(player.gameObject);
            logger.LogInfo("Your existing companion is following you.");
        }
    }
}
