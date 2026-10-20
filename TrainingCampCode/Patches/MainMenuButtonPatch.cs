using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// Adds a "Training Camp" button to the TOP-LEVEL main menu list (next to Singleplayer,
/// Multiplayer, Timeline, Settings...), not inside the singleplayer submenu.
/// Built by duplicating an existing NMainMenuTextButton so font, outline, hover/press animation
/// and spacing all come along for free, instead of a plain Button that only approximates the
/// look. The duplicate is made WITHOUT the Signals flag, so none of the original's click
/// handlers carry over - we wire our own from a clean slate.
///
/// A first attempt at this (using DuplicateFlags including UseInstantiation) made the button
/// vanish with no logged error. This version drops UseInstantiation (the likely culprit - it's
/// meant for nodes that are themselves instanced sub-scenes, which this button isn't) and
/// explicitly resets Visible/Modulate/Scale in case the source button was mid-animation when
/// duplicated, plus logs enough state to diagnose from the log file if it still doesn't show.
/// </summary>
[HarmonyPatch(typeof(NMainMenu), "_Ready")]
internal static class MainMenuButtonPatch
{
	private static void Postfix(NMainMenu __instance)
	{
		// Reaching the main menu means any Training Camp session is over - the sidebar
		// shouldn't still be sitting on screen (it used to persist until manually collapsed).
		NTrainingCampSidebar.Teardown();

		try
		{
			NMainMenuTextButton? singleplayerButton = __instance._singleplayerButton;
			if (singleplayerButton == null)
			{
				MainFile.Logger.Warn("Training Camp button: singleplayer button not found, skipping.");
				return;
			}

			Node? container = singleplayerButton.GetParent();
			if (container == null)
			{
				MainFile.Logger.Warn("Training Camp button: singleplayer button has no parent container.");
				return;
			}

			const int duplicateFlagsWithoutSignals = (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts);
			NMainMenuTextButton campButton = (NMainMenuTextButton)singleplayerButton.Duplicate(duplicateFlagsWithoutSignals);
			campButton.Name = "TrainingCampButton";

			// Defensive resets in case the source button was captured mid hover/press animation
			// (its Tween could leave Modulate/Scale at a transient, partially-invisible value).
			campButton.Visible = true;
			campButton.Modulate = Colors.White;
			campButton.Scale = Vector2.One;
			campButton.SelfModulate = Colors.White;

			container.AddChild(campButton);
			container.MoveChild(campButton, singleplayerButton.GetIndex() + 1);

			// _Ready() just ran (ConnectSignals() re-fetched `label` from the duplicated child),
			// so `label` is valid now. Clear `_locString` (carried over from whatever button we
			// duplicated) so a later locale-change notification can't make RefreshLabel() stomp
			// our manually-set text back to "单人模式"/whatever we copied from.
			campButton._locString = null;
			if (campButton.label != null)
			{
				campButton.label.SetTextAutoSize("训练营 Training Camp");
				campButton.label.Visible = true;
				campButton.label.Modulate = Colors.White;
			}
			campButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OnPressed(__instance)));

			MainFile.Logger.Info($"Training Camp button added to main menu. Visible={campButton.Visible} Size={campButton.Size} GlobalPos={campButton.GlobalPosition} ChildCount={campButton.GetChildCount()} LabelVisible={campButton.label?.Visible} LabelText={campButton.label?.Text}");
		}
		catch (Exception ex)
		{
			MainFile.Logger.Error($"Failed to add Training Camp button: {ex}");
		}
	}

	private static void OnPressed(NMainMenu mainMenu)
	{
		TrainingCampState.Active = true;
		NCharacterSelectScreen screen = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
		screen.InitializeSingleplayer();
		mainMenu.SubmenuStack.Push(screen);
	}
}
