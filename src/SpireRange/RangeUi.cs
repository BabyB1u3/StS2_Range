using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

internal static class RangeUi
{
    private sealed record Toolbar(NRun Owner, Label Status, Button Add, Button Reset, Button Exit,
        CenterContainer VictoryPanel, Button VictoryReset, Button VictoryExit);
    private static Toolbar? _toolbar;
    public static void AttachMenu(NMainMenu menu) => RangeMenu.Attach(menu);

    public static void AttachToolbar(NRun run)
    {
        var layer = new CanvasLayer { Name = "SpireRangeToolbar", Layer = 100 };
        run.AddChild(layer);
        var panel = new PanelContainer { Position = new Vector2(16, 100), CustomMinimumSize = new Vector2(260, 0) };
        layer.AddChild(panel);
        var box = new VBoxContainer();
        panel.AddChild(box);
        box.AddChild(new Label { Text = "靶场 / Practice Range" });
        box.AddChild(new Label { Text = "种子 / Seed: " + RangeSession.Config?.Seed });
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        box.AddChild(status);
        var error = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        box.AddChild(error);
        Task Reset() => RangeSession.Start(
            RangeSession.Config ?? throw new InvalidOperationException("Missing configuration."));
        var add = AddAction(box, "再加一张测试卡 / Add card", RangeSession.AddCard, error);
        var reset = AddAction(box, "重置同一测试 / Reset", Reset, error);
        var exit = AddAction(box, "退出靶场 / Exit", RangeSession.Exit, error);

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
        result.AddChild(new Label { Text = "测试胜利 / Victory", HorizontalAlignment = HorizontalAlignment.Center });
        result.AddChild(new Label { Text = "已击败全部敌人。请选择下一步。", HorizontalAlignment = HorizontalAlignment.Center });
        var victoryReset = AddAction(result, "重置同一测试 / Reset", Reset, error);
        var victoryExit = AddAction(result, "返回主菜单 / Main menu", RangeSession.Exit, error);
        _toolbar = new Toolbar(run, status, add, reset, exit, victory, victoryReset, victoryExit);
        run.TreeExiting += () => { if (ReferenceEquals(_toolbar?.Owner, run)) _toolbar = null; };
        RefreshToolbar(run);
    }

    public static void RefreshToolbar(NRun run)
    {
        var ui = _toolbar;
        if (ui is null || !ReferenceEquals(ui.Owner, run) || !GodotObject.IsInstanceValid(ui.Status)) return;
        var reason = RangeSession.AddCardDisabledReason();
        ui.Add.Disabled = reason is not null;
        ui.Add.TooltipText = reason ?? "将一张测试卡牌加入手牌。";
        var canLeave = RangeSession.CanResetOrExit;
        ui.Reset.Disabled = ui.Exit.Disabled = ui.VictoryReset.Disabled = ui.VictoryExit.Disabled = !canLeave;
        ui.Status.Text = RangeSession.BattleStatus switch
        {
            RangeBattleStatus.Victory => "测试胜利，已击败全部敌人。可重置或退出。",
            RangeBattleStatus.Defeat => "测试失败。可重置同一测试或退出。",
            _ => reason ?? "可正常出牌、结束回合。"
        };
        ui.VictoryPanel.Visible = RangeSession.IsActive && RangeSession.BattleStatus == RangeBattleStatus.Victory;
    }

    private static Button AddAction(VBoxContainer box, string text, Func<Task> action, Label status)
    {
        var button = new Button { Text = text };
        box.AddChild(button);
        button.Pressed += async () =>
        {
            if (RangeSession.IsBusy) return;
            status.Text = "";
            button.Disabled = true;
            await RunAction(action, status);
            if (GodotObject.IsInstanceValid(button)) button.Disabled = false;
            if (_toolbar is { } ui) RefreshToolbar(ui.Owner);
        };
        return button;
    }

    private static async Task RunAction(Func<Task> action, Label status)
    {
        try { await action(); }
        catch (Exception ex)
        {
            GD.PrintErr($"[SpireRange] {ex}");
            if (GodotObject.IsInstanceValid(status)) status.Text = "操作失败，详情见日志：" + ex.Message;
            else OS.Alert(ex.Message, "Spire Range");
        }
    }

}
