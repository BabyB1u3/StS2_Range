using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// RunManager.EnterAct(0, ...) normally ends by visibly entering either the Neow event (already
/// suppressed by SkipNeowEventPatch) or, once that's off, the map room itself - which still
/// flashes on screen for a moment before TrainingCampFlow.EnterDummyFight() redirects into
/// combat. While bootstrapping a training run, this replaces EnterAct's body with just the part
/// that actually sets up state (SetActInternal: current act index + map generation), skipping
/// the visual room-entry entirely - EnterDummyFight takes it straight into combat instead.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class SkipFirstRoomTransitionPatch
{
	private static bool Prefix(RunManager __instance, int currentActIndex, ref Task __result)
	{
		if (!TrainingCampState.IsBootstrapping)
		{
			return true;
		}
		__result = __instance.SetActInternal(currentActIndex);
		return false;
	}
}
