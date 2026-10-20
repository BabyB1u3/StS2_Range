using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// While the player is in the Training Camp dummy loop, combat ending (win or loss) should
/// never show the map/next-room screen - it should just drop them right back into a fresh
/// dummy fight. We hook both the win path (EndCombatInternal) and the loss path
/// (ProcessPendingLoss) on CombatManager.
/// </summary>
[HarmonyPatch(typeof(CombatManager), "EndCombatInternal")]
internal static class DummyLoopWinPatch
{
	private static void Postfix(ref Task __result)
	{
		if (TrainingCampState.InDummyLoop)
		{
			__result = ChainRestart(__result);
		}
	}

	private static async Task ChainRestart(Task original)
	{
		await original;
		await TrainingCampFlow.EnterDummyFight(TrainingCampState.CurrentTier);
	}
}

[HarmonyPatch(typeof(CombatManager), "ProcessPendingLoss")]
internal static class DummyLoopLossPatch
{
	private static void Postfix()
	{
		if (TrainingCampState.InDummyLoop)
		{
			TaskHelper.RunSafely(TrainingCampFlow.EnterDummyFight(TrainingCampState.CurrentTier));
		}
	}
}
