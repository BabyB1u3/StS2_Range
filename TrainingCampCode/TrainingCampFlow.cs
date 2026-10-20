using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace TrainingCamp.TrainingCampCode;

/// <summary>
/// Starts a real singleplayer run (same as a normal Standard run) for the chosen character, then
/// immediately drops the player into the game's own training-dummy encounter (the same one used
/// by the "Battleworn Dummy" event) instead of letting them land on the map, and opens the
/// Training Camp sidebar.
/// </summary>
public static class TrainingCampFlow
{
	public static async Task StartTrainingRun(NCharacterSelectScreen screen, string seed, List<ActModel> acts)
	{
		TrainingCampState.IsBootstrapping = true;
		try
		{
			CharacterModel character = screen._lobby.LocalPlayer.character;
			int ascension = screen._lobby.Ascension;
			await NGame.Instance!.Transition.FadeOut(0.8f, character.CharacterSelectTransitionPath);
			// Training Camp runs are throwaway sandboxes, not saved runs: shouldSave stays false so
			// nothing lands in the player's run history. This means save-dependent mods (e.g. a
			// "restart room" tool) won't have anything to restart into here - that's expected.
			await NGame.Instance!.StartNewSingleplayerRun(character, shouldSave: false, acts, Array.Empty<ModifierModel>(), seed, GameMode.Standard, ascension);
			screen.CleanUpLobby(disconnectSession: false);
		}
		catch (Exception ex)
		{
			MainFile.Logger.Error($"Training Camp run start failed: {ex}");
			return;
		}
		finally
		{
			TrainingCampState.IsBootstrapping = false;
		}

		await EnterDummyFight(BattlewornDummyEventEncounter.DummySetting.Setting1);
		NTrainingCampSidebar.Open();
	}

	/// <summary>
	/// Jumps into the game's own training-dummy encounter (a passive, non-attacking monster)
	/// at one of its three built-in HP tiers, and strips the event's turn-limit power since
	/// that's only meant for the original timed event, not free-form testing.
	/// </summary>
	public static async Task EnterDummyFight(BattlewornDummyEventEncounter.DummySetting setting)
	{
		try
		{
			TrainingCampState.CurrentTier = setting;
			TrainingCampState.InDummyLoop = true;

			BattlewornDummyEventEncounter encounter = (BattlewornDummyEventEncounter)ModelDb.Encounter<BattlewornDummyEventEncounter>().ToMutable();
			encounter.Setting = setting;
			await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Unassigned, encounter);

			Creature? dummy = CombatManager.Instance.DebugOnlyGetState().Enemies.FirstOrDefault();
			if (dummy != null)
			{
				await PowerCmd.Remove<BattlewornDummyTimeLimitPower>(dummy);
			}
		}
		catch (Exception ex)
		{
			MainFile.Logger.Error($"Training Camp dummy fight failed: {ex}");
		}
	}
}
