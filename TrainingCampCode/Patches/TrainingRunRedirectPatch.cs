using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// When the player reached character select through the Training Camp button, this replaces
/// the normal "start run -> go to map" behavior with "start run -> open dummy fight picker".
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), "StartNewSingleplayerRun", typeof(string), typeof(List<ActModel>))]
internal static class TrainingRunRedirectPatch
{
	private static bool Prefix(NCharacterSelectScreen __instance, string seed, List<ActModel> acts, ref Task __result)
	{
		if (!TrainingCampState.Active)
		{
			return true;
		}
		TrainingCampState.Active = false;
		__result = TrainingCampFlow.StartTrainingRun(__instance, seed, acts);
		return false;
	}
}
