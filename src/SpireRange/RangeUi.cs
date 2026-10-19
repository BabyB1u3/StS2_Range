using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireRange;

internal static class RangeUi
{
    private sealed record BattleUi(NRun Owner, MegaLabel Info, Control Left, Control Right,
        CenterContainer VictoryPanel, Button VictoryReset, Button VictoryExit);
    private static BattleUi? _battleUi;
    public static void AttachMenu(NMainMenu menu) => RangeMenu.Attach(menu);

    public static void AttachBattleUi(NRun run)
    {
        var bar = run.GlobalUi.TopBar;
        // Copy the native timer label's font, gold color, shadow and outline.
        // This is a sibling of the timer, so timer visibility never hides our seed.
        var info = (MegaLabel)bar.Timer.GetNode<MegaLabel>("TimerLabel").Duplicate(
            (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation));
        info.Name = "SpireRangeInfo";
        info.CustomMinimumSize = Vector2.Zero;
        info.MouseFilter = Control.MouseFilterEnum.Pass;
        info.HorizontalAlignment = HorizontalAlignment.Center;
        info.ClipText = true;
        info.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        bar.AddChild(info);
        info.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);

        var layer = new CanvasLayer { Name = "SpireRangeResult", Layer = 5 };
        run.AddChild(layer);
        var victory = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        layer.AddChild(victory);
        victory.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var resultPanel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        victory.AddChild(resultPanel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        resultPanel.AddChild(margin);
        var result = new VBoxContainer();
        result.AddThemeConstantOverride("separation", 16);
        margin.AddChild(result);
        result.AddChild(new Label { Text = "测试胜利", HorizontalAlignment = HorizontalAlignment.Center });
        result.AddChild(new Label { Text = "已击败全部敌人。", HorizontalAlignment = HorizontalAlignment.Center });
        var reset = RangeSetupScreen.ActionButton("重置靶场", true);
        var exit = RangeSetupScreen.ActionButton("退出靶场");
        result.AddChild(reset); result.AddChild(exit);
        reset.Pressed += () => _ = RunBattleAction(reset: true);
        exit.Pressed += () => _ = RunBattleAction(reset: false);
        _battleUi = new BattleUi(run, info, bar.GetNode<Control>("LeftAlignedStuff"),
            bar.GetNode<Control>("RightAlignedStuff"), victory, reset, exit);
        run.TreeExiting += () => { if (ReferenceEquals(_battleUi?.Owner, run)) _battleUi = null; };
        RefreshBattleUi(run);
    }

    public static void RefreshBattleUi(NRun run)
    {
        var ui = _battleUi;
        if (ui is null || !ReferenceEquals(ui.Owner, run) || !GodotObject.IsInstanceValid(ui.Info)) return;
        var bar = run.GlobalUi.TopBar;
        var inverse = bar.GetGlobalTransform().AffineInverse();
        var left = ui.Left.GetChildren().OfType<Control>().Where(c => c.Visible)
            .Select(c => (inverse * c.GetGlobalRect().End).X).DefaultIfEmpty(0).Max();
        var right = ui.Right.GetChildren().OfType<Control>().Where(c => c.Visible)
            .Select(c => (inverse * c.GlobalPosition).X).DefaultIfEmpty(bar.Size.X).Min();
        var width = Math.Max(0, right - left - 32);
        ui.Info.Position = new Vector2(left + 16, 0);
        ui.Info.Size = new Vector2(width, 80);
        var fontSize = width >= 440 ? 28 : 22;
        if (ui.Info.GetThemeFontSize("font_size") != fontSize) ui.Info.AddThemeFontSizeOverride("font_size", fontSize);
        var state = RangeSession.BattleStatus switch
        {
            RangeBattleStatus.Victory => " · 胜利",
            RangeBattleStatus.Defeat => " · 失败",
            _ => ""
        };
        ui.Info.Text = "靶场 · 种子：" + RangeSession.Config?.Seed + state;
        ui.Info.TooltipText = "靶场 · 种子：" + RangeSession.Config?.Seed + "\n按 Esc 可重置靶场或退出靶场。";
        var canLeave = RangeSession.CanResetOrExit;
        ui.VictoryReset.Disabled = ui.VictoryExit.Disabled = !canLeave;
        ui.VictoryPanel.Visible = RangeSession.IsActive && RangeSession.BattleStatus == RangeBattleStatus.Victory
            && NCapstoneContainer.Instance?.InUse != true;
        RangePauseMenu.RefreshAvailability();
    }

    internal static async Task RunBattleAction(bool reset)
    {
        if (!RangeSession.CanResetOrExit) return;
        try
        {
            // Release the native pause menu's input blocker before resetting the run.
            if (NCapstoneContainer.Instance?.InUse == true) NCapstoneContainer.Instance.Close();
            if (reset)
                await RangeSession.Start(RangeSession.Config ?? throw new InvalidOperationException("Missing configuration."));
            else
                await RangeSession.Exit();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireRange] {ex}");
            OS.Alert(ex.Message, "Spire Range");
        }
    }
}
