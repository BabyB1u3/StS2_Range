using MegaCrit.Sts2.Core.Models.Encounters;

namespace TrainingCamp.TrainingCampCode;

/// <summary>
/// Set while the player is going through character select by way of the Training Camp button,
/// so the run-start patch knows to redirect into a dummy fight instead of the normal map.
/// </summary>
public static class TrainingCampState
{
	public static bool Active;

	/// <summary>
	/// True once the player is inside the training dummy loop: whenever combat ends (win or
	/// loss) while this is true, we immediately re-enter a fresh dummy fight instead of letting
	/// the game show the map/next-room screen.
	/// </summary>
	public static bool InDummyLoop;

	/// <summary>
	/// True for the whole duration of StartNewSingleplayerRun -> EnterDummyFight. Used by
	/// SkipNeowEventPatch to suppress the normal Act-1-start "Neow" ancient-event screen, which
	/// would otherwise flash on screen for a moment before we redirect into the dummy fight.
	/// </summary>
	public static bool IsBootstrapping;

	public static BattlewornDummyEventEncounter.DummySetting CurrentTier = BattlewornDummyEventEncounter.DummySetting.Setting1;
}
