using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;

namespace SpireRange;

internal static class RangeCompendiumPickers
{
    private sealed class Picker
    {
        public required RangeSetupScreen Owner;
        public required NSubmenu Screen;
        public required NSubmenuStack Stack;
        public required Button Confirm;
        public required Label Caption;
        public CardModel? Card;
        public MonsterModel? Monster;
        public bool Open;
    }

    // Only fresh instances created here are adapted. The stack's normal Compendium
    // cached pages are never registered and retain all their original behavior.
    private static readonly Dictionary<NSubmenu, Picker> Pickers = new(ReferenceEqualityComparer.Instance);
    internal static bool IsPicker(NSubmenu screen) => Pickers.ContainsKey(screen);

    public static void OpenCards(RangeSetupScreen owner, NSubmenuStack stack)
    {
        var existing = Pickers.Values.FirstOrDefault(p => ReferenceEquals(p.Owner, owner) && p.Screen is NCardLibrary);
        if (existing is not null) { stack.Push(existing.Screen); return; }
        var screen = NCardLibrary.Create() ?? throw new InvalidOperationException("Card library unavailable.");
        screen.Name = "SpireRangeCardPicker";
        var picker = Register(owner, stack, screen, "选择一张卡牌查看详情，然后确认。", "使用这张卡牌");
        picker.Confirm.Pressed += () =>
        {
            if (picker.Card is null) return;
            owner.ChooseCard(picker.Card, NativeAccess.Get<NTickbox>(screen, "_viewUpgrades").IsTicked);
            stack.Pop();
        };
        stack.AddChild(screen);
        stack.Push(screen);
    }

    public static void OpenMonsters(RangeSetupScreen owner, NSubmenuStack stack)
    {
        var existing = Pickers.Values.FirstOrDefault(p => ReferenceEquals(p.Owner, owner) && p.Screen is NBestiary);
        if (existing is not null) { stack.Push(existing.Screen); return; }
        var screen = NBestiary.Create() ?? throw new InvalidOperationException("Bestiary unavailable.");
        screen.Name = "SpireRangeMonsterPicker";
        var picker = Register(owner, stack, screen, "浏览图鉴并预览怪物，然后确认。", "使用这个怪物");
        picker.Confirm.Pressed += () =>
        {
            if (picker.Monster is null) return;
            owner.ChooseMonster(picker.Monster);
            stack.Pop();
        };
        stack.AddChild(screen);
        stack.Push(screen);
    }

    private static Picker Register(RangeSetupScreen owner, NSubmenuStack stack, NSubmenu screen, string text, string action)
    {
        screen.Visible = false;
        var footer = new VBoxContainer { Name = "SpireRangeSelection", ZIndex = 20 };
        screen.AddChild(footer);
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        footer.OffsetLeft = -540; footer.OffsetTop = -132; footer.OffsetRight = -60; footer.OffsetBottom = -30;
        var caption = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 30), HorizontalAlignment = HorizontalAlignment.Center };
        footer.AddChild(caption);
        var confirm = RangeSetupScreen.ActionButton(action, true); confirm.Disabled = true; footer.AddChild(confirm);
        var picker = new Picker { Owner = owner, Screen = screen, Stack = stack, Caption = caption, Confirm = confirm };
        Pickers.Add(screen, picker);
        screen.TreeExiting += () =>
        {
            if (picker.Open) screen.OnSubmenuClosed();
            Pickers.Remove(screen);
        };
        return picker;
    }

    public static void Opened(NSubmenu screen)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return;
        picker.Open = true;
        if (screen is NCardLibrary library)
        {
            var filters = NativeAccess.Get<Dictionary<CharacterModel, NCardPoolFilter>>(library, "_cardPoolFilters");
            if (filters.TryGetValue(picker.Owner.SelectedCharacter, out var selected))
            {
                var pools = NativeAccess.Get<Dictionary<NCardPoolFilter, Func<CardModel, bool>>>(library, "_poolFilters");
                foreach (var filter in pools.Keys) filter.IsSelected = ReferenceEquals(filter, selected);
                NativeAccess.Call(library, "UpdateCardPoolFilter", selected);
            }
            var upgraded = NativeAccess.Get<NTickbox>(library, "_viewUpgrades");
            upgraded.IsTicked = picker.Owner.Upgraded;
            NativeAccess.Call(library, "ToggleShowUpgrades", upgraded);
        }
    }

    public static void Closing(NSubmenu screen)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return;
        picker.Open = false;
        if (screen is NCardLibrary library)
        {
            NativeAccess.Get<CancellationTokenSource?>(library, "_displayCardsShortDelayCancelToken")?.Cancel();
            NativeAccess.Set(library, "_lastHoveredControl", null);
            NativeAccess.Set(library, "_lastFocusedControl", null);
        }
    }

    public static void CloseFor(RangeSetupScreen owner)
    {
        foreach (var picker in Pickers.Values.Where(p => ReferenceEquals(p.Owner, owner)).ToArray())
        {
            if (GodotObject.IsInstanceValid(picker.Screen))
            {
                if (picker.Open) picker.Screen.OnSubmenuClosed();
                picker.Screen.QueueFree();
            }
            Pickers.Remove(picker.Screen);
        }
    }

    private static NCardLibrary? CardLibraryOf(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (current is NCardLibrary library) return IsPicker(library) ? library : null;
        return null;
    }

    public static void RefreshCardVisibility(NCardLibraryGrid grid)
    {
        if (CardLibraryOf(grid) is null) return;
        var cards = ModelDb.AllCards.ToArray();
        NativeAccess.Set(grid, "_seenCards", cards.Select(c => c.Id).ToHashSet());
        NativeAccess.Set(grid, "_unlockedCards", cards.ToHashSet());
        var all = NativeAccess.Get<List<CardModel>>(grid, "_allCards");
        all.Clear(); all.AddRange(cards);
    }

    public static bool SelectCard(NCardLibrary screen, NCardHolder holder)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return false;
        if (holder.CardModel is not { } card) return true;
        picker.Card = ModelDb.GetById<CardModel>(card.Id);
        NativeAccess.Set(screen, "_lastHoveredControl", holder);
        picker.Caption.Text = "已选：" + picker.Card.Title;
        picker.Confirm.Disabled = false;
        var grid = NativeAccess.Get<NCardLibraryGrid>(screen, "_grid");
        var cards = grid.VisibleCards.ToList();
        var index = cards.FindIndex(c => c.Id == picker.Card.Id);
        if (index >= 0)
            MegaCrit.Sts2.Core.Nodes.NGame.Instance!.GetInspectCardScreen().Open(cards, index,
                NativeAccess.Get<NTickbox>(screen, "_viewUpgrades").IsTicked);
        return true;
    }

    public static async Task DisplayCards(NCardLibrary screen)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return;
        NativeAccess.Get<CancellationTokenSource?>(screen, "_displayCardsShortDelayCancelToken")?.Cancel();
        await Task.Yield();
        if (!picker.Open || !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree()) return;
        var grid = NativeAccess.Get<NCardLibraryGrid>(screen, "_grid");
        grid.FilterCards(NativeAccess.Get<Func<CardModel, bool>>(screen, "_filter"),
            NativeAccess.Get<List<SortingOrders>>(screen, "_sortingPriority"));
        var count = NativeAccess.Get<LocString>(screen, "_cardCountLocString");
        count.Add("Amount", grid.VisibleCards.Count());
        NativeAccess.Get<MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel>(screen, "_cardCountLabel").Text =
            "[center]" + count.GetFormattedText() + "[/center]";
        NativeAccess.Get<Control>(screen, "_noResultsLabel").Visible = !grid.VisibleCards.Any();
    }

    public static void CreateMonsterEntries(NBestiary screen)
    {
        NativeAccess.Set(screen, "_discoveredMonsterIds", ModelDb.Monsters.Select(m => m.Id).ToHashSet());
        NativeAccess.Set(screen, "_discoveredEncounterIds", ModelDb.AllEncounters.Select(e => e.Id).ToHashSet());
        var list = NativeAccess.Get<VBoxContainer>(screen, "_bestiaryList");
        foreach (var act in ModelDb.Acts)
        {
            list.AddChild(NBestiaryActDivider.Create(act));
            foreach (var entry in EntriesForAct(act))
            {
                var row = NBestiaryEntry.Create(entry, isDiscovered: true);
                list.AddChild(row);
                row.Connect(NClickableControl.SignalName.Released,
                    Callable.From<NBestiaryEntry>(selected => NativeAccess.Call(screen, "OnMonsterClicked", selected)));
            }
        }
        var sidebar = NativeAccess.Get<NScrollableContainer>(screen, "_sidebar");
        var content = sidebar.GetNode<Control>("Content"); content.Position = new Vector2(content.Position.X, 0);
        sidebar.InstantlyScrollToTop();
        var picker = Pickers[screen];
        var entries = list.GetChildren().OfType<NBestiaryEntry>().ToArray();
        var first = entries.FirstOrDefault(e => e.Entry.monsterModel?.Id == picker.Owner.SelectedMonster?.Id) ?? entries.FirstOrDefault();
        if (first is not null) NativeAccess.Call(screen, "SelectMonster", first);
    }

    internal static List<BestiaryEntry> EntriesForAct(ActModel act)
    {
        var seen = new HashSet<ModelId>();
        var entries = new List<BestiaryEntry>();
        foreach (var encounter in act.AllEncounters)
            foreach (var monster in encounter.AllPossibleMonsters)
                if (seen.Add(monster.Id) && monster.ShouldShowInCompendium)
                    entries.Add(BestiaryEntry.FromMonster(monster, encounter, encounter.RoomType));
        if (act is Hive) entries.Add(BestiaryEntry.FromEncounter(ModelDb.Encounter<DecimillipedeElite>(), RoomType.Elite));
        entries.Sort((a, b) =>
        {
            var room = a.roomType.CompareTo(b.roomType);
            if (room != 0) return room;
            if (a.roomType == RoomType.Boss)
            {
                var encounter = string.Compare(a.GetEncounterTitle(), b.GetEncounterTitle(), StringComparison.CurrentCulture);
                if (encounter != 0) return encounter;
            }
            return string.Compare(a.GetEntryTitle(), b.GetEntryTitle(), StringComparison.CurrentCulture);
        });
        return entries;
    }

    public static void MonsterSelected(NBestiary screen, NBestiaryEntry entry)
    {
        if (!Pickers.TryGetValue(screen, out var picker)) return;
        picker.Monster = entry.Entry.monsterModel ?? entry.Entry.encounterModel.AllPossibleMonsters.FirstOrDefault();
        picker.Caption.Text = "已选：" + entry.Entry.GetEntryTitle();
        picker.Confirm.Disabled = picker.Monster is null;
    }
}
