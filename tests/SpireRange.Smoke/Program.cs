using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using SpireRange;

// Exercise the real Harmony patches against the installed game's SaveManager.
// No SaveManager constructor, save store, native Godot node, or user profile is opened.
var assembly = typeof(ModEntry).Assembly;
var session = assembly.GetType("SpireRange.RangeSession", throwOnError: true)!;
var active = session.GetField("<IsActive>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)!;
var harmony = new Harmony("SpireRange.Smoke");
foreach (var type in new[] { "SpireRange.SaveRunPatch", "SpireRange.ProgressIsolationPatch" })
    harmony.CreateClassProcessor(assembly.GetType(type, throwOnError: true)!).Patch();

var save = (SaveManager)RuntimeHelpers.GetUninitializedObject(typeof(SaveManager));
try
{
    active.SetValue(null, true);
    await save.SaveRun(null);
    save.DeleteCurrentRun();
    save.SaveRunHistory(null!);
    save.UpdateProgressAfterCombatWon(null!, null!);
    save.UpdateProgressWithRunData(null!, false);
    save.SaveProgressFile();
    Console.WriteLine("PASS: active range skips run save, deletion, progress updates, and progress file writes.");

    active.SetValue(null, false);
    try
    {
        save.SaveProgressFile();
        throw new Exception("Normal game progress saving was unexpectedly intercepted.");
    }
    catch (NullReferenceException)
    {
        Console.WriteLine("PASS: outside the range, the original SaveManager method executes.");
    }

    await LifecycleSmoke.Run(assembly, harmony);
    GameOverSmoke.Run(assembly, save, harmony);
    EncounterSmoke.Run(assembly);
    BattleStateSmoke.Run(assembly);
    SeedSmoke.Run(assembly);
    PickerSmoke.Run(assembly, harmony);
    PauseMenuSmoke.Run(assembly, harmony);
    CharacterPickerSmoke.Run(assembly, harmony);
}
finally
{
    active.SetValue(null, false);
    harmony.UnpatchAll("SpireRange.Smoke");
}
