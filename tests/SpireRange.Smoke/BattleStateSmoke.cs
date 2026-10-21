using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class BattleStateSmoke
{
    public static void Run(Assembly mod)
    {
        var session = mod.GetType("SpireRange.RangeSession")!;
        AccessTools.Method(session, "Initialize").Invoke(null, null);
        var stateField = AccessTools.Field(session, "_state");
        var active = AccessTools.Field(session, "<IsActive>k__BackingField");
        var status = AccessTools.Property(session, "BattleStatus");
        var config = AccessTools.Field(session, "<Config>k__BackingField");
        var oldState = stateField.GetValue(null);
        var oldConfig = config.GetValue(null);
        var oldStatus = status.GetValue(null);
        try
        {
            var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            var combat = new CombatState(runState: run);
            var room = new CombatRoom(combat);
            stateField.SetValue(null, run);
            active.SetValue(null, true);
            config.SetValue(null, Activator.CreateInstance(mod.GetType("SpireRange.RangeConfig")!,
                new object?[] { null, Array.CreateInstance(mod.GetType("SpireRange.RangeTestCard")!, 0), null, 3, "STATE_TEST" }));
            void Raise<T>(string name, T value) =>
                ((Action<T>)AccessTools.Field(typeof(CombatManager), name).GetValue(CombatManager.Instance)!)(value);

            Raise("CombatSetUp", combat);
            var unrelatedRun = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            Raise("CombatWon", new CombatRoom(new CombatState(runState: unrelatedRun)));
            if (status.GetValue(null)!.ToString() != "Playing")
                throw new Exception("An unrelated combat changed the practice result.");
            Raise("CombatWon", room);
            Raise("CombatEnded", room);
            if (status.GetValue(null)!.ToString() != "Victory")
                throw new Exception("Victory was lost when the combat-ended event followed combat-won.");
            Raise("CombatSetUp", combat);
            Raise("CombatEnded", room);
            if (status.GetValue(null)!.ToString() != "Defeat")
                throw new Exception("Combat ending without victory did not preserve defeat.");
            Console.WriteLine("PASS: native combat events preserve victory/defeat and ignore unrelated combats.");
        }
        finally
        {
            stateField.SetValue(null, oldState);
            config.SetValue(null, oldConfig);
            AccessTools.Field(session, "<BattleStatus>k__BackingField").SetValue(null, oldStatus);
            active.SetValue(null, false);
        }
    }
}
