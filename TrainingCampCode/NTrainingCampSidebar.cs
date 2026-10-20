using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace TrainingCamp.TrainingCampCode;

/// <summary>
/// In-game sidebar (docked to the right edge, not a separate floating window) for testing
/// cards/skins against the game's own training dummy: pick a dummy HP tier, open the visual
/// card editor, grant energy, and set the dummy's current HP directly.
/// </summary>
public partial class NTrainingCampSidebar : CanvasLayer
{
	private static NTrainingCampSidebar? _instance;

	private PanelContainer _panel = null!;
	private Label _statusLabel = null!;
	private LineEdit _energyInput = null!;
	private LineEdit _hpInput = null!;

	public static void Open()
	{
		if (_instance != null)
		{
			_instance._panel.Visible = true;
			return;
		}
		NGame? game = NGame.Instance;
		if (game == null)
		{
			MainFile.Logger.Warn("Cannot open Training Camp sidebar: NGame.Instance is null.");
			return;
		}

		NTrainingCampSidebar sidebar = new();
		sidebar.Build();
		game.GetTree().Root.AddChild(sidebar);
		_instance = sidebar;
	}

	/// <summary>
	/// Fully removes the sidebar, rather than just hiding it. Called whenever the main menu
	/// loads (see MainMenuButtonPatch), since reaching the main menu means the training camp
	/// session has ended and the sidebar shouldn't still be sitting on screen.
	/// </summary>
	public static void Teardown()
	{
		if (_instance == null)
		{
			return;
		}
		_instance.QueueFree();
		_instance = null;
	}

	private void Build()
	{
		Layer = 50;

		// Always-visible tab on the right edge; toggles the panel even after it's been closed.
		Button toggleTab = new()
		{
			Text = "训\n练\n营",
			AnchorLeft = 1f,
			AnchorRight = 1f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -32,
			OffsetRight = 0,
			OffsetTop = -70,
			OffsetBottom = 70,
		};
		toggleTab.Pressed += () => _panel.Visible = !_panel.Visible;
		AddChild(toggleTab);

		_panel = new PanelContainer
		{
			AnchorLeft = 1f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = -340,
			OffsetRight = -32,
			OffsetTop = 0,
			OffsetBottom = 0,
		};
		AddChild(_panel);

		ScrollContainer scroll = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_panel.AddChild(scroll);

		VBoxContainer root = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		root.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(root);

		Label title = new() { Text = "训练营 Training Camp" };
		root.AddChild(title);

		Button closeButton = new() { Text = "收起面板 (点右边小条可重新打开)" };
		closeButton.Pressed += () => _panel.Visible = false;
		root.AddChild(closeButton);

		root.AddChild(new HSeparator());

		Label dummyLabel = new() { Text = "训练假人 HP 档位" };
		root.AddChild(dummyLabel);

		OptionButton dummyOption = new();
		dummyOption.AddItem("简单 (75 HP)", 0);
		dummyOption.AddItem("中等 (150 HP)", 1);
		dummyOption.AddItem("困难 (300 HP)", 2);
		dummyOption.ItemSelected += OnDummyTierSelected;
		root.AddChild(dummyOption);

		root.AddChild(new HSeparator());

		AddButtonRow(root, "编辑卡组 / 加卡牌", OnOpenCardEditorPressed);

		root.AddChild(new HSeparator());

		_energyInput = AddRow(root, "能量", "3", "加能量", OnAddEnergyPressed);
		_hpInput = AddRow(root, "假人血量", "100", "设置血量", OnSetEnemyHpPressed);

		root.AddChild(new HSeparator());

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		root.AddChild(_statusLabel);
	}

	private static LineEdit AddRow(VBoxContainer root, string labelText, string placeholder, string buttonText, Action onPressed)
	{
		Label label = new() { Text = labelText };
		root.AddChild(label);

		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 6);

		LineEdit input = new()
		{
			PlaceholderText = placeholder,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		row.AddChild(input);

		Button button = new() { Text = buttonText };
		button.Pressed += onPressed;
		row.AddChild(button);

		root.AddChild(row);
		return input;
	}

	private static void AddButtonRow(VBoxContainer root, string buttonText, Action onPressed)
	{
		Button button = new() { Text = buttonText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		button.Pressed += onPressed;
		root.AddChild(button);
	}

	private void OnDummyTierSelected(long index)
	{
		BattlewornDummyEventEncounter.DummySetting setting = index switch
		{
			0 => BattlewornDummyEventEncounter.DummySetting.Setting1,
			1 => BattlewornDummyEventEncounter.DummySetting.Setting2,
			_ => BattlewornDummyEventEncounter.DummySetting.Setting3,
		};
		TaskHelper.RunSafely(TrainingCampFlow.EnterDummyFight(setting));
		ShowResult(new CmdResult(success: true, "正在切换训练假人..."));
	}

	/// <summary>
	/// Prefers the in-combat player (needed to add cards/energy to hand), falling back to the
	/// run-level player when only a run (no combat) is active.
	/// </summary>
	private static Player? GetPlayer()
	{
		if (CombatManager.Instance.IsInProgress)
		{
			return LocalContext.GetMe(CombatManager.Instance.DebugOnlyGetState());
		}
		if (RunManager.Instance.IsInProgress)
		{
			return LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
		}
		return null;
	}

	private void OnOpenCardEditorPressed()
	{
		Player? player = GetPlayer();
		if (player == null)
		{
			ShowResult(new CmdResult(success: false, "请先开始一局游戏"));
			return;
		}
		NCardDeckEditor.Open(RunManager.Instance.DebugOnlyGetState());
	}

	private void OnAddEnergyPressed()
	{
		string amount = _energyInput.Text.Trim();
		Player? player = GetPlayer();
		if (player == null)
		{
			ShowResult(new CmdResult(success: false, "请先进入战斗"));
			return;
		}
		CmdResult result = new EnergyConsoleCmd().Process(player, new[] { amount });
		if (result.task != null)
		{
			TaskHelper.RunSafely(result.task);
		}
		ShowResult(result);
	}

	private void OnSetEnemyHpPressed()
	{
		if (!CombatManager.Instance.IsInProgress)
		{
			ShowResult(new CmdResult(success: false, "当前不在战斗中"));
			return;
		}
		if (!int.TryParse(_hpInput.Text.Trim(), out int hp) || hp < 1)
		{
			ShowResult(new CmdResult(success: false, "血量必须是正整数"));
			return;
		}
		Creature? enemy = CombatManager.Instance.DebugOnlyGetState().Enemies.FirstOrDefault();
		if (enemy == null)
		{
			ShowResult(new CmdResult(success: false, "没有找到敌人"));
			return;
		}
		enemy.MaxHp = hp;
		enemy.CurrentHp = hp;
		ShowResult(new CmdResult(success: true, $"已将 {enemy.Monster?.Id.Entry ?? "敌人"} 血量设为 {hp}"));
	}

	private void ShowResult(CmdResult result)
	{
		_statusLabel.Text = (result.success ? "[成功] " : "[失败] ") + result.msg;
	}
}
