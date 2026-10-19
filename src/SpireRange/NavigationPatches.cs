using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace SpireRange;

// A practice battle must not turn into a normal map run through the vanilla UI.
[HarmonyPatch]
internal static class RangeNavigationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterMapCoord));
        yield return AccessTools.Method(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen));
        yield return AccessTools.Method(typeof(CombatRoom), nameof(CombatRoom.OfferRoomEndRewards));
    }

    private static bool Prefix(ref Task __result)
    {
        if (!RangeSession.IsActive) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
