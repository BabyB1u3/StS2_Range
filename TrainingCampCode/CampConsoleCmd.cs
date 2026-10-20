using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace TrainingCamp.TrainingCampCode;

/// <summary>
/// Dev console command that opens the Training Camp window.
/// Auto-registered by the game via AbstractConsoleCmd's [GenerateSubtypes] discovery.
/// </summary>
public class CampConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "camp";

	public override string Args => "";

	public override string Description => "Opens the Training Camp window for testing cards/mods.";

	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		NTrainingCampSidebar.Open();
		return new CmdResult(success: true, "Opened Training Camp sidebar.");
	}
}
