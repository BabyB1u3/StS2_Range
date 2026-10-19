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
    public static void AttachMenu(NMainMenu menu)
    {
        RangeSession.OnMainMenuReached();
        var button = new Button
        {
            Name = "SpireRangeEntry", Text = "靶场 / Practice Range",
            Position = new Vector2(24, 24), CustomMinimumSize = new Vector2(270, 48), ZIndex = 100
        };
        menu.AddChild(button);
        button.Pressed += () => OpenSetup(menu);
    }

    private static void OpenSetup(NMainMenu menu)
    {
        if (menu.HasNode("SpireRangeSetup")) return;
        var overlay = new ColorRect
        {
            Name = "SpireRangeSetup", Color = new Color(0, 0, 0, 0.9f), ZIndex = 110
        };
        menu.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(800, 0) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(760, Math.Clamp(menu.GetViewportRect().Size.Y - 140, 360, 850)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        margin.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(box);
        box.AddChild(new Label { Text = "靶场 / Practice Range", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label { Text = "临时单人战斗 · 不保存本局进度 · 单个怪物模式为实验功能" });

        var characters = ModelDb.AllCharacters.ToList();
        var cards = ModelDb.AllCards.OrderBy(c => c.Id.Entry).ToList();
        var monsters = ModelDb.Monsters.OrderBy(m => m.Id.Entry).ToList();
        var character = AddChoice(box, "角色 / Character", characters.Select(c => Name(c, "characters")));

        var cardGroups = new List<(string Label, HashSet<ModelId>? Ids)>
        {
            ("全部卡牌 / All cards", null)
        };
        foreach (var owner in characters)
            cardGroups.Add((Name(owner, "characters"), owner.CardPool.AllCards
                .Concat(owner.StartingDeck).Select(c => c.Id).ToHashSet()));
        var characterCardIds = cardGroups.Skip(1).SelectMany(g => g.Ids!).ToHashSet();
        cardGroups.Add(("通用及特殊卡牌 / Shared & special", cards.Select(c => c.Id)
            .Where(id => !characterCardIds.Contains(id)).ToHashSet()));
        var cardGroup = AddChoice(box, "按角色筛选卡牌 / Card character", cardGroups.Select(g => g.Label));
        var cardFilter = new LineEdit { PlaceholderText = "搜索卡牌名称 / ID" };
        box.AddChild(cardFilter);
        var cardChoice = AddChoice(box, "卡牌 / Card", cards.Select(c => Name(c, "cards")));

        var monsterGroups = new List<(string Label, HashSet<ModelId>? Ids)>
        {
            ("全部地图 / All maps", null)
        };
        foreach (var act in ModelDb.Acts)
            monsterGroups.Add((Name(act, "acts"), act.AllMonsters.Select(m => m.Id).ToHashSet()));
        var monsterGroup = AddChoice(box, "按地图筛选怪物 / Monster map", monsterGroups.Select(g => g.Label));
        var monsterFilter = new LineEdit { PlaceholderText = "搜索怪物名称 / ID" };
        box.AddChild(monsterFilter);
        var monsterChoice = AddChoice(box, "怪物 / Monster", monsters.Select(m => Name(m, "monsters")));
        var visibleCards = cards.ToList();
        var visibleMonsters = monsters.ToList();
        void RefreshCards()
        {
            var selected = cardChoice.Selected >= 0 ? visibleCards[cardChoice.Selected] : null;
            var ids = cardGroups[cardGroup.Selected].Ids;
            visibleCards = cards.Where(c => (ids is null || ids.Contains(c.Id)) &&
                Name(c, "cards").Contains(cardFilter.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            Fill(cardChoice, visibleCards.Select(c => Name(c, "cards")));
            var index = selected is null ? -1 : visibleCards.FindIndex(c => c.Id == selected.Id);
            if (index >= 0) cardChoice.Select(index);
        }
        void RefreshMonsters()
        {
            var selected = monsterChoice.Selected >= 0 ? visibleMonsters[monsterChoice.Selected] : null;
            var ids = monsterGroups[monsterGroup.Selected].Ids;
            visibleMonsters = monsters.Where(m => (ids is null || ids.Contains(m.Id)) &&
                Name(m, "monsters").Contains(monsterFilter.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            Fill(monsterChoice, visibleMonsters.Select(m => Name(m, "monsters")));
            var index = selected is null ? -1 : visibleMonsters.FindIndex(m => m.Id == selected.Id);
            if (index >= 0) monsterChoice.Select(index);
        }
        cardFilter.TextChanged += _ => RefreshCards();
        cardGroup.ItemSelected += _ => RefreshCards();
        monsterFilter.TextChanged += _ => RefreshMonsters();
        monsterGroup.ItemSelected += _ => RefreshMonsters();
        var upgraded = new CheckBox { Text = "使用升级版卡牌 / Upgraded card" };
        box.AddChild(upgraded);
        box.AddChild(new Label { Text = "每回合基础能量 / Base energy per turn" });
        var energy = new SpinBox { MinValue = 0, MaxValue = 99, Step = 1, Value = 3 };
        box.AddChild(energy);
        box.AddChild(new Label { Text = "随机种子：留空则新测试随机；重置保留本次种子。" });
        var seed = new LineEdit { PlaceholderText = "留空随机 / Optional seed" };
        box.AddChild(seed);
        box.AddChild(new Label { Text = "初始牌组：角色初始卡组 + 1 张所选卡牌；保留角色初始遗物及机制。" });
        var status = new Label { Text = "部分怪物依赖同伴或专属场景；首版尚未逐一验证。" };
        box.AddChild(status);
        var start = new Button { Text = "开始测试 / Start", CustomMinimumSize = new Vector2(0, 44) };
        box.AddChild(start);
        start.Pressed += async () =>
        {
            if (character.Selected < 0 || cardChoice.Selected < 0 || monsterChoice.Selected < 0)
            {
                status.Text = "请清除搜索条件并选择角色、卡牌和怪物。";
                return;
            }
            var config = new RangeConfig(characters[character.Selected], visibleCards[cardChoice.Selected],
                visibleMonsters[monsterChoice.Selected], upgraded.ButtonPressed, (int)energy.Value,
                seed.Text.Trim());
            start.Disabled = true;
            await RunAction(() => RangeSession.Start(config), status);
            if (GodotObject.IsInstanceValid(start)) start.Disabled = false;
        };
        var close = new Button { Text = "返回 / Back" };
        box.AddChild(close);
        close.Pressed += () => { if (!RangeSession.IsBusy) overlay.QueueFree(); };
    }

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

    private static OptionButton AddChoice(VBoxContainer box, string label, IEnumerable<string> items)
    {
        box.AddChild(new Label { Text = label });
        var choice = new OptionButton { CustomMinimumSize = new Vector2(680, 36), FitToLongestItem = false };
        box.AddChild(choice);
        Fill(choice, items);
        return choice;
    }

    private static void Fill(OptionButton choice, IEnumerable<string> items)
    {
        choice.Clear();
        foreach (var item in items) choice.AddItem(item);
        choice.Disabled = choice.ItemCount == 0;
        if (choice.ItemCount > 0) choice.Select(0);
    }

    private static string Name(AbstractModel model, string table)
    {
        var suffix = table == "monsters" ? ".name" : ".title";
        var loc = new LocString(table, model.Id.Entry + suffix);
        return loc.Exists() ? $"{loc.GetFormattedText()} [{model.Id.Entry}]" : model.Id.Entry;
    }
}
