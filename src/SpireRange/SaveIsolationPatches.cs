using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace SpireRange;

// Native combat victory calls SaveRun even when RunManager.ShouldSave is false.
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveRun))]
internal static class SaveRunPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (!RangeSession.IsActive) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class ProgressIsolationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[]
        {
            nameof(SaveManager.DeleteCurrentRun),
            nameof(SaveManager.UpdateProgressAfterCombatWon),
            nameof(SaveManager.UpdateProgressWithRunData),
            nameof(SaveManager.SaveProgressFile)
        })
            yield return AccessTools.Method(typeof(SaveManager), name)
                ?? throw new MissingMethodException(typeof(SaveManager).FullName, name);
    }

    private static bool Prefix() => !RangeSession.IsActive;
}
