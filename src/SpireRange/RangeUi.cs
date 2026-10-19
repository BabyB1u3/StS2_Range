using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireRange;

internal static class RangeUi
{
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
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 0) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);
        box.AddChild(new Label { Text = "靶场 / Practice Range", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new Label { Text = "临时单人战斗 · 不保存本局进度 · 单个怪物模式为实验功能" });

        var characters = ModelDb.AllCharacters.ToList();
        var cards = ModelDb.AllCards.OrderBy(c => c.Id.Entry).ToList();
        var monsters = ModelDb.Monsters.OrderBy(m => m.Id.Entry).ToList();
        var character = AddChoice(box, "角色 / Character", characters.Select(c => Name(c, "characters")));
        var cardFilter = new LineEdit { PlaceholderText = "搜索卡牌名称 / ID" };
        box.AddChild(cardFilter);
        var cardChoice = AddChoice(box, "卡牌 / Card", cards.Select(c => Name(c, "cards")));
        var monsterFilter = new LineEdit { PlaceholderText = "搜索怪物名称 / ID" };
        box.AddChild(monsterFilter);
        var monsterChoice = AddChoice(box, "怪物 / Monster", monsters.Select(m => Name(m, "monsters")));
        var visibleCards = cards.ToList();
        var visibleMonsters = monsters.ToList();
        cardFilter.TextChanged += query =>
        {
            visibleCards = cards.Where(c => Name(c, "cards").Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            Fill(cardChoice, visibleCards.Select(c => Name(c, "cards")));
        };
        monsterFilter.TextChanged += query =>
        {
            visibleMonsters = monsters.Where(m => Name(m, "monsters").Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            Fill(monsterChoice, visibleMonsters.Select(m => Name(m, "monsters")));
        };
        var upgraded = new CheckBox { Text = "使用升级版卡牌 / Upgraded card" };
        box.AddChild(upgraded);
        box.AddChild(new Label { Text = "每回合基础能量 / Base energy per turn" });
        var energy = new SpinBox { MinValue = 0, MaxValue = 99, Step = 1, Value = 3 };
        box.AddChild(energy);
        var seed = new LineEdit { Text = "SPIRE_RANGE", PlaceholderText = "随机种子 / Seed" };
        box.AddChild(seed);
        box.AddChild(new Label { Text = "初始牌组：5 张所选卡牌；保留角色初始遗物及机制。" });
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
                string.IsNullOrWhiteSpace(seed.Text) ? "SPIRE_RANGE" : seed.Text.Trim());
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
        var status = new Label { Text = "可正常出牌、结束回合。" };
        box.AddChild(status);
        AddAction(box, "再加一张测试卡 / Add card", RangeSession.AddCard, status);
        AddAction(box, "重置同一测试 / Reset", () => RangeSession.Start(
            RangeSession.Config ?? throw new InvalidOperationException("Missing configuration.")), status);
        AddAction(box, "退出靶场 / Exit", RangeSession.Exit, status);
    }

    private static void AddAction(VBoxContainer box, string text, Func<Task> action, Label status)
    {
        var button = new Button { Text = text };
        box.AddChild(button);
        button.Pressed += async () =>
        {
            if (RangeSession.IsBusy) return;
            button.Disabled = true;
            await RunAction(action, status);
            if (GodotObject.IsInstanceValid(button)) button.Disabled = false;
        };
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
