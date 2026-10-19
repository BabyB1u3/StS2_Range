using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace SpireRange;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.StartCombatInternal))]
internal static class RangeCombatStartPatch
{
    private static void Postfix(CombatManager __instance, ref Task __result)
    {
        if (RangeSession.IsActive)
            __result = RangeSession.ObserveCombatStart(__result, __instance.DebugOnlyGetState());
    }
}
