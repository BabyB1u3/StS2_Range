using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SpireRange;

[HarmonyPatch(typeof(NRun), nameof(NRun.ShowGameOverScreen))]
internal static class RangeGameOverPatch
{
    private static void Prefix(SerializableRun serializableRun)
    {
        if (!RangeSession.IsActive) return;
        RangeLocalization.Register();
        // Unsaved runs skip the normal history creation. The death screen otherwise
        // receives an empty history and displays the game's debugging fallback.
        // SaveRunHistory is blocked during practice; only the in-memory history is used.
        var manager = RunManager.Instance;
        CreateHistory(serializableRun, manager.NetService.Platform);
    }

    internal static void CreateHistory(SerializableRun run, PlatformType platform) =>
        RunHistoryUtilities.CreateRunHistoryEntry(run, victory: false, isAbandoned: false, platform);
}
