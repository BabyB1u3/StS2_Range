using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// Normally Act 1 always starts with the "Neow" ancient-event screen (choose a starting boon)
/// before the map is shown. Training Camp doesn't want that - it just flashes on screen for a
/// moment before we redirect into the dummy fight. RunManager.SetStartedWithNeowFlag() is the
/// single place that decides whether this run gets that screen, so we force it off while
/// bootstrapping a training run.
/// </summary>
[HarmonyPatch(typeof(RunManager), "SetStartedWithNeowFlag")]
internal static class SkipNeowEventPatch
{
	private static void Postfix(RunManager __instance)
	{
		if (TrainingCampState.IsBootstrapping && __instance.State != null)
		{
			__instance.State.ExtraFields.StartedWithNeow = false;
		}
	}
}
