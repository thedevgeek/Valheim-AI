using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimCompanion
{
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class CompanionDamagePatch
    {
        private const float LucyBiteBonusDamage = 15f;
        private static readonly HashSet<int> ReportedAllowedTargets = new HashSet<int>();
        private static readonly HashSet<int> ReportedBlockedTargets = new HashSet<int>();
        private static readonly Dictionary<int, int> ReportedDamageResults = new Dictionary<int, int>();

        private sealed class DamageTrace
        {
            public float HealthBefore;
        }

        [HarmonyPrefix]
        private static bool Prefix(Character __instance, HitData hit, ref DamageTrace? __state)
        {
            if (CompanionController.IsTaggedCompanion(__instance))
            {
                return false;
            }

            if (hit == null)
            {
                return true;
            }

            var attacker = hit.GetAttacker();
            if (!CompanionController.IsTaggedCompanion(attacker))
            {
                return true;
            }

            if (!CompanionController.CanDamageThreat(__instance, attacker))
            {
                if (ReportedBlockedTargets.Add(__instance.GetInstanceID()))
                {
                    CompanionController.LogDamageDecision(__instance, false);
                }

                return false;
            }

            if (ReportedAllowedTargets.Add(__instance.GetInstanceID()))
            {
                CompanionController.LogDamageDecision(__instance, true);
            }

            __state = new DamageTrace
            {
                HealthBefore = __instance.GetHealth()
            };

            var damage = hit.m_damage;
            if (damage.m_slash >= damage.m_blunt && damage.m_slash >= damage.m_pierce)
            {
                damage.m_slash += LucyBiteBonusDamage;
            }
            else if (damage.m_blunt >= damage.m_pierce)
            {
                damage.m_blunt += LucyBiteBonusDamage;
            }
            else
            {
                damage.m_pierce += LucyBiteBonusDamage;
            }

            hit.m_damage = damage;
            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(Character __instance, HitData hit, DamageTrace? __state)
        {
            if (__state == null)
            {
                return;
            }

            var instanceId = __instance.GetInstanceID();
            ReportedDamageResults.TryGetValue(instanceId, out var reportCount);
            if (reportCount >= 5)
            {
                return;
            }

            ReportedDamageResults[instanceId] = reportCount + 1;
            var healthAfter = __instance.GetHealth();
            var damage = hit.m_damage;
            CompanionController.LogDamageResult(
                __instance,
                __state.HealthBefore,
                healthAfter,
                damage.m_blunt,
                damage.m_slash,
                damage.m_pierce);
        }
    }
}
