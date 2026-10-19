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
        foreach (var patch in new[] { typeof(CleanupBoundary), typeof(NetworkBoundary) })
            harmony.CreateClassProcessor(patch).Patch();

        var session = mod.GetType("SpireRange.RangeSession", throwOnError: true)!;
        var active = AccessTools.Field(session, "<IsActive>k__BackingField");
        var stop = AccessTools.Method(session, "Stop");
        var combatState = AccessTools.Field(typeof(CombatManager), "_state");
        var synchronizer = AccessTools.Field(typeof(RunManager), "<ActionQueueSynchronizer>k__BackingField");
        var previousSync = synchronizer.GetValue(RunManager.Instance);
        var rangeState = AccessTools.Field(session, "_state");
        var oldRangeState = rangeState.GetValue(null);
        var startupError = AccessTools.Property(session, "StartupError");
        var starting = AccessTools.Field(typeof(CombatManager), "<IsStarting>k__BackingField");
        synchronizer.SetValue(RunManager.Instance,
            RuntimeHelpers.GetUninitializedObject(typeof(ActionQueueSynchronizer)));
        try
        {
            foreach (var returnToMenu in new[] { true, false, false, true })
            {
                combatState.SetValue(CombatManager.Instance, new CombatState());
                active.SetValue(null, true);
                await (Task)stop.Invoke(null, new object[] { returnToMenu })!;
                if (combatState.GetValue(CombatManager.Instance) is not null)
                    throw new Exception("Exiting the range left stale combat state; the next battle would fail.");
                if ((bool)active.GetValue(null)!)
                    throw new Exception("Exiting the range left the session active.");
            }
            Console.WriteLine("PASS: exit and direct-reset teardowns clear the game's real combat state for the next battle.");

            harmony.CreateClassProcessor(mod.GetType("SpireRange.RangeCombatStartPatch")!).Patch();
            var observe = AccessTools.Method(session, "ObserveCombatStart");
            var canLeave = AccessTools.Property(session, "CanResetOrExit");
            async Task Fail(CombatState combat)
            {
                try
                {
                    await (Task)observe.Invoke(null, new object[]
                    {
                        Task.FromException(new InvalidOperationException("No valid next state found.")), combat
                    })!;
                    throw new Exception("Startup errors were swallowed instead of preserving native logging.");
                }
                catch (InvalidOperationException ex) when (ex.Message == "No valid next state found.") { }
            }
            foreach (var returnToMenu in new[] { true, false })
            {
                var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
                var combat = new CombatState(runState: run);
                rangeState.SetValue(null, run);
                combatState.SetValue(CombatManager.Instance, combat);
                active.SetValue(null, true);
                starting.SetValue(CombatManager.Instance, true);
                if (canLeave.GetValue(null) is not false) throw new Exception("Normal startup no longer blocks teardown.");
                await Fail(new CombatState(runState: (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState))));
                if (startupError.GetValue(null) is not null) throw new Exception("Unrelated startup fault unlocked this run.");
                await Fail(combat);
                if (startupError.GetValue(null) is not "No valid next state found." || canLeave.GetValue(null) is not true)
                    throw new Exception("Faulted startup left reset/exit unavailable.");
                AccessTools.Method(session, "EnsureIdle").Invoke(null, null);
                if (returnToMenu) await (Task)AccessTools.Method(session, "Exit").Invoke(null, null)!;
                else await (Task)stop.Invoke(null, new object[] { false })!;
                if (CombatManager.Instance.IsStarting || combatState.GetValue(CombatManager.Instance) is not null
                    || startupError.GetValue(null) is not null || active.GetValue(null) is not false)
                    throw new Exception("Startup recovery left combat or error state behind.");
            }
            Console.WriteLine("PASS: failed native startup preserves its exception, unlocks scoped recovery, and clears stale starting state on exit/reset.");
        }
        finally
        {
            active.SetValue(null, false);
            combatState.SetValue(CombatManager.Instance, null);
            rangeState.SetValue(null, oldRangeState);
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

    [HarmonyPatch(typeof(ActionQueueSynchronizer), nameof(ActionQueueSynchronizer.SetCombatState))]
    private static class NetworkBoundary
    {
        private static bool Prefix() => false;
    }
}
