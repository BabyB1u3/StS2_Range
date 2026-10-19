using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using SpireRange;

internal static class GameOverSmoke
{
    public static void Run(Assembly mod, SaveManager save, Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(HistoryLogBoundary)).Patch();
        var active = AccessTools.Field(mod.GetType("SpireRange.RangeSession"), "<IsActive>k__BackingField");
        var state = AccessTools.Field(typeof(RunManager), "<State>k__BackingField");
        var release = AccessTools.Field(typeof(ReleaseInfoManager), "_instance");
        var previousState = state.GetValue(RunManager.Instance);
        var previousHistory = RunManager.Instance.History;
        var previousRelease = release.GetValue(null);
        try
        {
            // No constructor for the file-backed managers is run.
            SaveManager.MockInstanceForTesting(save);
            release.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(ReleaseInfoManager)));
            state.SetValue(RunManager.Instance, RuntimeHelpers.GetUninitializedObject(typeof(RunState)));
            RunManager.Instance.History = null;
            active.SetValue(null, true);
            var encounterId = ModelDb.GetId<RangeEncounter>();
            var run = new SerializableRun
            {
                Players = new List<SerializablePlayer>(),
                SerializableRng = new SerializableRunRngSet { Seed = "RANGE_DEATH_TEST" },
                MapPointHistory = new List<List<MapPointHistoryEntry>>
                {
                    new() { new MapPointHistoryEntry { Rooms = new()
                    {
                        new MapPointRoomHistoryEntry { RoomType = RoomType.Monster, ModelId = encounterId }
                    } } }
                }
            };
            AccessTools.Method(mod.GetType("SpireRange.RangeGameOverPatch"), "CreateHistory")
                .Invoke(null, new object[] { run, default(PlatformType) });
            var history = RunManager.Instance.History ?? throw new Exception("Practice death history was not created.");
            if (history.KilledByEncounter != encounterId || history.Seed != run.SerializableRng.Seed ||
                NRunHistory.GetGameOverType(history) != GameOverType.CombatDeath)
                throw new Exception("Practice death still resolves to the game's debugging fallback.");

            var entries = AccessTools.Method(mod.GetType("SpireRange.RangeLocalization"), "Entries");
            foreach (var language in new[] { "zhs", "eng" })
            {
                var values = (Dictionary<string, string>)entries.Invoke(null, new object[] { encounterId.Entry, language })!;
                var table = new LocTable("encounters", values);
                if (!table.HasEntry(encounterId.Entry + ".loss") ||
                    string.IsNullOrWhiteSpace(table.GetRawText(encounterId.Entry + ".loss")))
                    throw new Exception("Practice death quote is missing.");
            }
            Console.WriteLine("PASS: practice death produces in-memory combat history and localized loss text without a save store.");
        }
        finally
        {
            active.SetValue(null, false);
            state.SetValue(RunManager.Instance, previousState);
            RunManager.Instance.History = previousHistory;
            release.SetValue(null, previousRelease);
            AccessTools.Field(typeof(SaveManager), "_mockInstance").SetValue(null, null);
        }
    }

    // Logging initializes Godot's native runtime. Replace only this test's log call;
    // leave the history creation and save interception running unchanged.
    private static void SkipNativeLog(string message, int line) { }

    [HarmonyPatch(typeof(RunHistoryUtilities), nameof(RunHistoryUtilities.CreateRunHistoryEntry))]
    private static class HistoryLogBoundary
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method &&
                    method.DeclaringType?.FullName == "MegaCrit.Sts2.Core.Logging.Log" && method.Name == "Info")
                    instruction.operand = AccessTools.Method(typeof(GameOverSmoke), nameof(SkipNativeLog));
                yield return instruction;
            }
        }
    }
}
