using HarmonyLib;

namespace ValheimCompanion
{
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class CompanionTargetSelectionPatch
    {
        [HarmonyPostfix]
        private static void AfterUpdateTarget(MonsterAI __instance)
        {
            CompanionController.PreserveCombatTarget(__instance);
        }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class CompanionCombatPatch
    {
        [HarmonyPostfix]
        private static void AfterUpdateAI(MonsterAI __instance)
        {
            CompanionController.PreserveCombatTarget(__instance);
        }
    }
}
