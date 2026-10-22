using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace SpireRange;

[HarmonyPatch(typeof(BattleFriendV1), nameof(BattleFriendV1.AfterAddedToRoom))]
internal static class RangeDummyTimeLimitPatch
{
    private static bool Prefix(BattleFriendV1 __instance, ref Task __result)
    {
        if (!RangeSession.IsDefaultDummyCombat(__instance.Creature.CombatState)) return true;
        // In this game build the native hook only applies the three-turn escape
        // power. Skip it for our default dummy. Combat startup runs asynchronously,
        // so removing it immediately after EnterRoomDebug can race its application.
        __result = Task.CompletedTask;
        return false;
    }
}
