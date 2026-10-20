using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace TrainingCamp.TrainingCampCode;

/// <summary>
/// Reuses the game's own card library screen (same one used by the compendium) as a visual
/// card picker: click cards to select/highlight them, then confirm to add them all to hand at
/// once (any that don't fit in hand automatically spill into the deck instead).
/// Opened manually (not through the normal main-menu submenu stack), so its own back/close
/// button is not wired up - use our own close/confirm buttons instead.
/// </summary>
public static class NCardDeckEditor
{
	// Background + library are inserted directly into NGame's own tree (right before its
	// InspectionContainer), NOT a separate CanvasLayer. CanvasLayers always draw above the
	// default layer regardless of sibling order, which is where the game's own "card preview"
	// popup lives (NGame's InspectionContainer) - putting our stuff in its own CanvasLayer made
	// it permanently cover that popup. Inserting as plain siblings just before the container
	// keeps our normal screen-layering: above the 3D combat scene, below the native preview.
	private static Control? _background;
	private static Control? _libraryHost;
	// The toolbar stays in its own CanvasLayer so close/confirm remain usable even while a
	// native card preview is open on top of the library.
	private static CanvasLayer? _toolbarLayer;

	private static readonly Dictionary<CardModel, NCardHolder> _selected = new();
	private static MegaLabel _countLabel = null!;
	private static Button _modeButton = null!;
	private static bool _selectionModeOn;

	/// <summary>Read by SuppressCardPreviewPatch to skip the native card-detail popup entirely
	/// while selection mode is on, instead of letting it flash open and then closing it.</summary>
	public static bool IsSelectionModeActive => _selectionModeOn;

	private const float SelectedScale = 1.18f;
	private const float SelectedLift = 22f;
	private static readonly Color SelectedTint = new(0.55f, 1f, 0.6f, 1f);

	public static void Open(IRunState runState)
	{
		if (_background != null)
		{
			_background.Visible = true;
			_libraryHost!.Visible = true;
			_toolbarLayer!.Visible = true;
			return;
		}

		NGame? game = NGame.Instance;
		if (game == null)
		{
			MainFile.Logger.Warn("Cannot open card editor: NGame.Instance is null.");
			return;
		}

		NCardLibrary? library = NCardLibrary.Create();
		if (library == null)
		{
			MainFile.Logger.Warn("Cannot open card editor: NCardLibrary.Create() returned null.");
			return;
		}

		Control background = new()
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		ColorRect backgroundFill = new()
		{
			Color = new Color(0.05f, 0.05f, 0.08f, 1f),
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		background.AddChild(backgroundFill);

		Control libraryHost = new()
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
		};
		libraryHost.AddChild(library);

		// Insert both right before the InspectionContainer, in order, so draw order becomes:
		// ...normal game content..., background, libraryHost, InspectionContainer, ...
		int insertIndex = game._inspectionContainer.GetIndex();
		game.AddChild(background);
		game.MoveChild(background, insertIndex);
		game.AddChild(libraryHost);
		game.MoveChild(libraryHost, insertIndex + 1);

		CanvasLayer toolbarLayer = new() { Layer = 65 };

		// Sits in the library's own left sidebar column, just below its filter panel
		// (type/rarity/cost/alphabet sort) - that's empty space, so nothing gets covered.
		VBoxContainer column = new()
		{
			AnchorLeft = 0f,
			AnchorRight = 0f,
			AnchorTop = 0.655f,
			AnchorBottom = 0.655f,
			OffsetLeft = 14,
			OffsetRight = 270,
			OffsetTop = 0,
			OffsetBottom = 96,
		};
		column.AddThemeConstantOverride("separation", 8);
		toolbarLayer.AddChild(column);

		// Long bar matching the filter header style (teal, full width), toggled on/off.
		_modeButton = CreateBarButton("点击开始选择加入卡牌", new Color(0.16f, 0.42f, 0.44f, 1f));
		_modeButton.ToggleMode = true;
		_modeButton.Toggled += OnModeToggled;
		_modeButtonLabel = _modeButton.GetChild<MegaLabel>(0);
		column.AddChild(_modeButton);

		HBoxContainer closeConfirmRow = new();
		closeConfirmRow.AddThemeConstantOverride("separation", 8);
		column.AddChild(closeConfirmRow);

		Button closeButton = CreateBarButton("✕ 关闭编辑器", new Color(0.6f, 0.2f, 0.2f, 1f));
		closeButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		closeButton.Pressed += Close;
		closeConfirmRow.AddChild(closeButton);

		Button confirmButton = CreateBarButton("✓ 加入手牌", new Color(0.18f, 0.5f, 0.28f, 1f));
		confirmButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		confirmButton.Pressed += OnConfirmPressed;
		closeConfirmRow.AddChild(confirmButton);

		// Selection count + instructions, top-center.
		_countLabel = new MegaLabel
		{
			Text = "已选择 0 张卡",
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = 70,
			OffsetTop = 14,
			OffsetRight = -70,
			OffsetBottom = 50,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_countLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_countLabel.AddThemeConstantOverride("outline_size", 6);
		toolbarLayer.AddChild(_countLabel);

		game.GetTree().Root.AddChild(toolbarLayer);

		// Initialize AFTER the library is in the scene tree: calling it before _Ready() has run
		// left its internal card grid unresolved, so the very first open showed no cards until
		// something else (e.g. switching character) forced a re-populate.
		library.Initialize(runState);
		// Initialize() only stores the run state - the actual first population normally happens
		// in OnSubmenuOpened(), called by the menu stack when a screen is pushed. We bypass that
		// stack entirely, so nothing ever populated the grid on the very first open (switching a
		// filter afterwards indirectly called DisplayCards() and "fixed" it). Call it ourselves.
		library.OnSubmenuOpened();

		library._grid.Connect(NCardGrid.SignalName.HolderPressed, Callable.From<NCardHolder>(OnCardPressed));

		_background = background;
		_libraryHost = libraryHost;
		_toolbarLayer = toolbarLayer;
	}

	private static void Close()
	{
		if (_background == null)
		{
			return;
		}
		_background.Visible = false;
		_libraryHost!.Visible = false;
		_toolbarLayer!.Visible = false;
	}

	private static MegaLabel? _modeButtonLabel;

	/// <summary>
	/// A full-width rounded bar matching the look of the library's own filter headers
	/// (type/rarity/cost/alphabet), rather than Godot's default button style. Text is a MegaLabel
	/// child - MegaLabel applies the game's real font substitution itself in _Ready(), which a
	/// plain Button's own Text property does not pick up correctly.
	/// </summary>
	private static Button CreateBarButton(string text, Color color)
	{
		Button button = new() { CustomMinimumSize = new Vector2(0, 40) };
		SetBarButtonColor(button, color);

		MegaLabel label = new()
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
		};
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 6);
		button.AddChild(label);
		return button;
	}

	private static void SetBarButtonColor(Button button, Color color)
	{
		StyleBoxFlat normal = new() { BgColor = color, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
		StyleBoxFlat hover = new() { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
		StyleBoxFlat pressed = new() { BgColor = color.Darkened(0.15f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
		StyleBoxFlat onHover = new() { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };

		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("hover_pressed", onHover);
	}

	private static readonly Color ModeOnBarColor = new(0.25f, 0.55f, 0.85f, 1f);

	private static void OnModeToggled(bool pressed)
	{
		_selectionModeOn = pressed;
		SetBarButtonColor(_modeButton, pressed ? ModeOnBarColor : new Color(0.16f, 0.42f, 0.44f, 1f));
		if (_modeButtonLabel != null)
		{
			_modeButtonLabel.SetTextAutoSize(pressed ? "选择模式中 - 点击卡牌选中 (再点此处退出)" : "点击开始选择加入卡牌");
		}
	}

	private static void OnCardPressed(NCardHolder holder)
	{
		// Plain click (selection mode off, the default) = just the native preview, already
		// wired to this same signal. Only acts as a selector while the mode button is toggled
		// on, so the two actions stay separate instead of always happening together.
		if (!_selectionModeOn)
		{
			return;
		}

		CardModel? model = holder.CardModel;
		if (model == null)
		{
			return;
		}

		if (_selected.Remove(model))
		{
			holder.Scale = Vector2.One;
			holder.Modulate = Colors.White;
			holder.Position -= new Vector2(0, -SelectedLift);
		}
		else
		{
			_selected[model] = holder;
			holder.PivotOffset = holder.Size / 2f;
			holder.Scale = new Vector2(SelectedScale, SelectedScale);
			holder.Modulate = SelectedTint;
			holder.Position += new Vector2(0, -SelectedLift);
		}
		_countLabel.SetTextAutoSize($"已选择 {_selected.Count} 张卡");
	}

	private static void OnConfirmPressed()
	{
		if (_selected.Count == 0)
		{
			Close();
			return;
		}
		Player? player = GetPlayer();
		if (player == null)
		{
			MainFile.Logger.Warn("Card editor: no active player to add cards to.");
			return;
		}

		List<CardModel> toAdd = new(_selected.Keys);
		foreach (NCardHolder holder in _selected.Values)
		{
			holder.Scale = Vector2.One;
			holder.Modulate = Colors.White;
			holder.Position -= new Vector2(0, -SelectedLift);
		}
		_selected.Clear();
		_countLabel.SetTextAutoSize("已选择 0 张卡");

		TaskHelper.RunSafely(AddCardsAsync(player, toAdd));
		Close();
	}

	private static async Task AddCardsAsync(Player player, List<CardModel> models)
	{
		ICardScope scope = CombatManager.Instance.IsInProgress
			? CombatManager.Instance.DebugOnlyGetState()
			: RunManager.Instance.DebugOnlyGetState();

		foreach (CardModel model in models)
		{
			CardModel card = scope.CreateCard(model, player);
			bool handHasRoom = PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand;
			PileType pile = handHasRoom ? PileType.Hand : PileType.Deck;
			await CardPileCmd.Add(card, pile);
		}
	}

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
}
