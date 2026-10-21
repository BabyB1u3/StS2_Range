using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

// A real submenu: the game's stack owns visibility, back navigation and focus.
public partial class RangeSetupScreen : NSubmenu
{
    private NMainMenu _menu = null!;
    private Button _changeCharacter = null!;
    private Label _characterName = null!;
    private SpinBox _energy = null!;
    private LineEdit _seed = null!;
    private Label _cardName = null!;
    private Label _monsterName = null!;
    private Button _start = null!;
    private bool _blocking;

    internal CardModel? SelectedCard { get; private set; }
    internal MonsterModel? SelectedMonster { get; private set; }
    internal CharacterModel SelectedCharacter { get; private set; } = null!;
    internal bool Upgraded { get; private set; }
    protected override Control InitialFocusedControl => _changeCharacter;

    public static RangeSetupScreen Create(NMainMenu menu, CharacterModel character)
    {
        var screen = new RangeSetupScreen { Name = "SpireRangeSetup", Visible = false,
            _menu = menu, SelectedCharacter = character };
        screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Copy only the native back-button node; no native page or persistent signals
        // from the Compendium are attached to this setup page.
        var template = NCardLibrary.Create() ?? throw new InvalidOperationException("Card library scene unavailable.");
        var back = (NBackButton)template.GetNode<NBackButton>("BackButton").Duplicate(
            (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
        template.Free();
        screen.AddChild(back);
        return screen;
    }

    public override void _Ready()
    {
        Build();
        ConnectSignals();
    }

    public override void OnSubmenuOpened()
    {
        NHotkeyManager.Instance?.AddBlockingScreen(this);
        _blocking = true;
    }

    public override void OnSubmenuClosed()
    {
        base.OnSubmenuClosed();
        ReleaseInput();
        RangeCompendiumPickers.CloseFor(this);
        QueueFree();
    }

    public override void _ExitTree()
    {
        ReleaseInput();
        RangeCompendiumPickers.CloseFor(this);
    }

    private void ReleaseInput()
    {
        if (!_blocking) return;
        NHotkeyManager.Instance?.RemoveBlockingScreen(this);
        _blocking = false;
    }

    internal void ChooseCard(CardModel card, bool upgraded)
    {
        SelectedCard = ModelDb.GetById<CardModel>(card.Id);
        Upgraded = upgraded && SelectedCard.IsUpgradable;
        _cardName.Text = SelectedCard.Title + (Upgraded ? "（已升级）" : "");
        _start.Disabled = SelectedMonster is null;
    }

    internal void ChooseCharacter(CharacterModel character)
    {
        SelectedCharacter = character;
        _characterName.Text = character.Title.GetFormattedText();
    }

    internal void ChooseMonster(MonsterModel monster)
    {
        SelectedMonster = ModelDb.GetById<MonsterModel>(monster.Id);
        _monsterName.Text = SelectedMonster.Title.GetFormattedText();
        _start.Disabled = SelectedCard is null;
    }

    private void Build()
    {
        var font = _menu.GetNode<NMainMenuTextButton>("MainMenuTextButtons/MultiplayerButton")
            .label!.GetThemeFont("font");
        Theme = new Theme { DefaultFont = font, DefaultFontSize = 24 };
        var backdrop = new ColorRect { Color = new Color("111923"), MouseFilter = MouseFilterEnum.Stop };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MoveChild(backdrop, 0);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.OffsetLeft = 180; panel.OffsetTop = 70; panel.OffsetRight = -180; panel.OffsetBottom = -140;
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 32);
        panel.AddChild(margin);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        margin.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 22);
        scroll.AddChild(body);
        var heading = Text("靶场", 42); heading.AddThemeColorOverride("font_color", new Color("EACB88"));
        body.AddChild(heading);
        body.AddChild(Text("选择角色、卡牌和怪物，直接开始一场练习。", 22));
        body.AddChild(Text("角色", 24));
        _characterName = Text(SelectedCharacter.Title.GetFormattedText(), 28);
        body.AddChild(_characterName);
        _changeCharacter = ActionButton("更换角色");
        _changeCharacter.Pressed += () => RangeCharacterPicker.Open(_menu, this);
        body.AddChild(_changeCharacter);
        var choices = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choices.AddThemeConstantOverride("h_separation", 28);
        choices.AddThemeConstantOverride("v_separation", 22);
        body.AddChild(choices);
        void ResizeLayout()
        {
            var inset = Math.Clamp(Size.X * 0.07f, 24, 180);
            panel.OffsetLeft = inset; panel.OffsetRight = -inset;
            choices.Columns = Size.X < 1200 ? 1 : 2;
        }
        Resized += ResizeLayout;
        ResizeLayout();
        _cardName = ChoiceTile(choices, "测试卡牌", "尚未选择卡牌", "打开卡牌总览",
            () => RangeCompendiumPickers.OpenCards(this, _stack));
        _monsterName = ChoiceTile(choices, "目标怪物", "尚未选择怪物", "打开怪物图鉴",
            () => RangeCompendiumPickers.OpenMonsters(this, _stack));
        var options = new HBoxContainer(); options.AddThemeConstantOverride("separation", 16); body.AddChild(options);
        var energyLabel = Text("每回合基础能量", 22);
        energyLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        energyLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        options.AddChild(energyLabel);
        _energy = new SpinBox { MinValue = 0, MaxValue = 99, Value = 3, Step = 1, CustomMinimumSize = new Vector2(150, 48) };
        options.AddChild(_energy);
        _seed = new LineEdit { PlaceholderText = "随机种子（选填，留空则随机）", CustomMinimumSize = new Vector2(0, 50) };
        body.AddChild(_seed);
        body.AddChild(Text("保留角色初始卡组和遗物，额外加入一张测试卡牌。重置会保留本次种子。", 20));
        _start = ActionButton("开始测试", true); _start.Disabled = true;
        _start.Pressed += StartBattle;
        body.AddChild(_start);
        // Native back button stays above the backdrop and uses its normal hotkeys.
        MoveChild(GetNode("BackButton"), GetChildCount() - 1);
    }

    private async void StartBattle()
    {
        if (SelectedCard is null || SelectedMonster is null || RangeSession.IsBusy) return;
        var config = new RangeConfig(SelectedCharacter, SelectedCard, SelectedMonster,
            Upgraded, (int)_energy.Value, _seed.Text.Trim());
        _start.Disabled = true;
        HideBackButtonImmediately();
        try
        {
            await NGame.Instance!.Transition.FadeOut();
            _stack.Pop();
            await RangeSession.Start(config);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireRange] {ex}");
            OS.Alert(ex.Message, "Spire Range");
        }
    }

    private static Label ChoiceTile(Container parent, string title, string empty, string buttonText, Action open)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", PanelStyle()); parent.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 16); panel.AddChild(box);
        box.AddChild(Text(title, 26));
        var selected = Text(empty, 24); selected.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selected.CustomMinimumSize = new Vector2(100, 70); box.AddChild(selected);
        var button = ActionButton(buttonText); box.AddChild(button); button.Pressed += open;
        return selected;
    }

    internal static Button ActionButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(320, 56) };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var style = PanelStyle();
            style.BgColor = new Color(state == "hover" ? "39404A" : primary ? "443A29" : "242D38");
            style.BorderColor = new Color(state == "disabled" ? "555963" : "C4A267");
            button.AddThemeStyleboxOverride(state, style);
        }
        button.AddThemeColorOverride("font_color", new Color("F6E7C6"));
        return button;
    }

    private static Label Text(string text, int size)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill }; label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("EFE6D6")); return label;
    }

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("202833"), BorderColor = new Color("766449"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 18, ContentMarginBottom = 18
    };
}
