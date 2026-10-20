using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace TrainingCamp.TrainingCampCode.Patches;

/// <summary>
/// While the card editor's selection mode is on, clicking a card still fires the library's own
/// "show card detail" handler (same signal we use for selection), which flashes the native
/// zoomed-in preview open for a frame. Closing it afterwards still showed a visible flash, so
/// instead this skips the native preview from opening at all while selection mode is active.
/// </summary>
[HarmonyPatch(typeof(NInspectCardScreen), "Open", typeof(List<CardModel>), typeof(int), typeof(bool))]
internal static class SuppressCardPreviewPatch
{
	private static bool Prefix()
	{
		return !NCardDeckEditor.IsSelectionModeActive;
	}
}
