using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

internal static class LifecycleSmoke
{
    public static async Task Run(Assembly mod, Harmony harmony)
    {
        // Run the mod's actual Stop path and the game's real CombatManager.Reset.
        // Replace UI/resource/network boundaries so no running game or profile is needed.
        foreach (var patch in new[] { typeof(CleanupBoundary), typeof(PreloadBoundary), typeof(NetworkBoundary) })
            harmony.CreateClassProcessor(patch).Patch();

        var session = mod.GetType("SpireRange.RangeSession", throwOnError: true)!;
        var active = AccessTools.Field(session, "<IsActive>k__BackingField");
        var stop = AccessTools.Method(session, "Stop");
        var combatState = AccessTools.Field(typeof(CombatManager), "_state");
        var synchronizer = AccessTools.Field(typeof(RunManager), "<ActionQueueSynchronizer>k__BackingField");
        var previousSync = synchronizer.GetValue(RunManager.Instance);
        synchronizer.SetValue(RunManager.Instance,
            RuntimeHelpers.GetUninitializedObject(typeof(ActionQueueSynchronizer)));
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                combatState.SetValue(CombatManager.Instance, new CombatState());
                active.SetValue(null, true);
                await (Task)stop.Invoke(null, null)!;
                if (combatState.GetValue(CombatManager.Instance) is not null)
                    throw new Exception("Exiting the range left stale combat state; the next battle would fail.");
                if ((bool)active.GetValue(null)!)
                    throw new Exception("Exiting the range left the session active.");
            }
            Console.WriteLine("PASS: three range teardowns clear the game's real combat state for the next battle.");
        }
        finally
        {
            active.SetValue(null, false);
            combatState.SetValue(CombatManager.Instance, null);
            synchronizer.SetValue(RunManager.Instance, previousSync);
        }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    private static class CleanupBoundary
    {
        private static bool Prefix(bool graceful)
        {
            CombatManager.Instance.Reset(graceful);
            return false;
        }
    }

    [HarmonyPatch(typeof(PreloadManager), nameof(PreloadManager.LoadMainMenuEssentials))]
    private static class PreloadBoundary
    {
        private static bool Prefix(ref Task __result)
        {
            __result = Task.CompletedTask;
            return false;
        }
    }

    [HarmonyPatch(typeof(ActionQueueSynchronizer), nameof(ActionQueueSynchronizer.SetCombatState))]
    private static class NetworkBoundary
    {
        private static bool Prefix() => false;
    }
}
