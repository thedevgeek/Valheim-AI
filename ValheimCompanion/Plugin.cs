using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace ValheimCompanion
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.valheimai.companion";
        public const string PluginName = "Valheim Companion";
        public const string PluginVersion = "0.40.0";

        private ConfigEntry<KeyboardShortcut>? _fightShortcut;
        private ConfigEntry<KeyboardShortcut>? _gatherShortcut;
        private ConfigEntry<KeyboardShortcut>? _stopShortcut;
        private ConfigEntry<KeyboardShortcut>? _statusShortcut;
        private ConfigEntry<KeyboardShortcut>? _summonShortcut;
        private ConfigEntry<KeyboardShortcut>? _dismissShortcut;
        private bool _summonInProgress;
        private float _nextCompanionUpdate;
        private float _nextAppearanceUpdate;

        private void Awake()
        {
            _fightShortcut = Config.Bind(
                "Commands",
                "Fight",
                new KeyboardShortcut(KeyCode.F6),
                "Defend you from nearby enemies targeting you or Lucy. Requires a summoned companion and host authority.");
            _gatherShortcut = Config.Bind(
                "Commands",
                "Gather",
                new KeyboardShortcut(KeyCode.F7),
                "Collect nearby dropped items and harvestable plants into your inventory. Requires the host-owned companion.");
            _stopShortcut = Config.Bind(
                "Commands",
                "Stop",
                new KeyboardShortcut(KeyCode.F8),
                "Stop the prototype mode.");
            _statusShortcut = Config.Bind(
                "Commands",
                "Status",
                new KeyboardShortcut(KeyCode.F9),
                "Report the current prototype mode and companion state in the BepInEx log.");
            _summonShortcut = Config.Bind(
                "Companion",
                "Summon",
                new KeyboardShortcut(KeyCode.F10),
                "Spawn or recall your vanilla wolf companion. Host-only in multiplayer.");
            _dismissShortcut = Config.Bind(
                "Companion",
                "Dismiss",
                new KeyboardShortcut(KeyCode.F11),
                "Dismiss your summoned wolf companion. This removes it from the test world.");

            new Harmony(PluginGuid).PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Lucy damage rules installed: she is immune to damage and only damages nearby enemies threatening her owner or Lucy, with +15 bite damage per strike.");
            Logger.LogInfo("Valheim Companion loaded. Hotkeys control a host-owned vanilla wolf; console commands are not patched.");
        }

        private void Update()
        {
            if (_fightShortcut!.Value.IsDown())
            {
                CompanionModeState.Set(CompanionMode.Fight);
                Logger.LogInfo("Fight mode selected. The companion will defend you from nearby enemies targeting you or Lucy.");
            }

            if (_gatherShortcut!.Value.IsDown())
            {
                CompanionModeState.Set(CompanionMode.Gather);
                Logger.LogInfo("Gather mode selected. The companion will collect nearby dropped items and harvestable plants.");
            }

            if (_stopShortcut!.Value.IsDown())
            {
                CompanionModeState.Set(CompanionMode.Stopped);
                Logger.LogInfo("Companion mode stopped.");
            }

            if (_statusShortcut!.Value.IsDown())
            {
                CompanionController.LogStatus(CompanionModeState.Current, Logger);
            }

            if (_summonShortcut!.Value.IsDown() && !_summonInProgress)
            {
                StartCoroutine(SummonCompanion());
            }

            if (_dismissShortcut!.Value.IsDown())
            {
                CompanionController.Dismiss(Logger);
            }

            if (Time.unscaledTime >= _nextCompanionUpdate)
            {
                _nextCompanionUpdate = Time.unscaledTime + 0.5f;
                CompanionController.UpdateMode(CompanionModeState.Current, Logger);
            }

            if (Time.unscaledTime >= _nextAppearanceUpdate)
            {
                _nextAppearanceUpdate = Time.unscaledTime + 1f;
                CompanionController.UpdateAppearances(Logger);
            }
        }

        private IEnumerator SummonCompanion()
        {
            _summonInProgress = true;
            yield return CompanionController.Summon(Logger);
            _summonInProgress = false;
        }
    }
}
