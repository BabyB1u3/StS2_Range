using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;

internal static class MultiCardSmoke
{
    public static void Run(Assembly mod)
    {
        ModelDb.Inject(typeof(StrikeIronclad));
        ModelDb.Inject(typeof(DefendIronclad));
        ModelDb.Inject(typeof(Wound));
        var strike = ModelDb.Card<StrikeIronclad>();
        var defend = ModelDb.Card<DefendIronclad>();
        var wound = ModelDb.Card<Wound>();
        var selectionType = mod.GetType("SpireRange.RangeTestCard")!;
        object Selection(CardModel card, bool upgraded) => Activator.CreateInstance(selectionType, card, upgraded)!;
        var source = Array.CreateInstance(selectionType, 4);
        source.SetValue(Selection(strike, false), 0);
        source.SetValue(Selection(strike, true), 1);
        source.SetValue(Selection(defend, true), 2);
        source.SetValue(Selection(wound, true), 3);
        var config = Activator.CreateInstance(mod.GetType("SpireRange.RangeConfig")!,
            new object?[] { null, source, null, 3, "MULTI_CARD_TEST" })!;
        var cards = (IList)AccessTools.Property(config.GetType(), "Cards").GetValue(config)!;
        source.SetValue(Selection(wound, false), 0);
        if (!cards.IsReadOnly || !ReferenceEquals(AccessTools.Property(selectionType, "Card").GetValue(cards[0]), strike))
            throw new Exception("Practice configuration did not snapshot the selection for resets.");

        var add = AccessTools.Method(mod.GetType("SpireRange.RangeSession"), "AddTestCards");
        CardModel[]? previous = null;
        for (var reset = 0; reset < 3; reset++)
        {
            // Use the game's actual CreateCard, upgrade and deck insertion code;
            // only initialize the state needed for this inventory-only test.
            var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            AccessTools.Field(typeof(RunState), "_allCards").SetValue(run, new List<CardModel>());
            AccessTools.Field(typeof(RunState), "_players").SetValue(run, new List<Player> { player });
            AccessTools.Field(typeof(Player), "<Deck>k__BackingField").SetValue(player, new CardPile(PileType.Deck));
            AccessTools.Field(typeof(Player), "_runState").SetValue(player, run);
            var starting = run.CreateCard(strike, player);
            player.Deck.AddInternal(starting, silent: true);
            var empty = Array.CreateInstance(selectionType, 0);
            add.Invoke(null, new object[] { run, player, empty });
            if (player.Deck.Cards.Count != 1 || !ReferenceEquals(player.Deck.Cards.Single(), starting) || starting.IsUpgraded)
                throw new Exception("An empty test-card selection changed the starting deck.");
            add.Invoke(null, new[] { run, player, (object)cards });
            var deck = player.Deck.Cards.ToArray();
            if (deck.Length != 5 || !ReferenceEquals(deck[0], starting) || starting.IsUpgraded
                || deck[1].Id != strike.Id || deck[1].IsUpgraded
                || deck[2].Id != strike.Id || !deck[2].IsUpgraded
                || deck[3].Id != defend.Id || !deck[3].IsUpgraded
                || deck[4].Id != wound.Id || deck[4].IsUpgraded)
                throw new Exception("Multi-card setup lost a copy, changed the starting card, or applied the wrong upgrades.");
            if (deck.Distinct(ReferenceEqualityComparer.Instance).Count() != 5
                || deck.Any(c => !run.ContainsCard(c) || !ReferenceEquals(c.Owner, player))
                || previous is not null && deck.Any(c => previous.Contains(c)))
                throw new Exception("Test cards reused mutable instances or were not registered with their run and player.");
            previous = deck;
        }
        if (strike.IsUpgraded || defend.IsUpgraded || wound.IsUpgraded)
            throw new Exception("Multi-card setup mutated the canonical card models.");
        Console.WriteLine("PASS: multi-card snapshots preserve duplicates, per-copy upgrades, starting cards and fresh run-owned instances across resets.");
    }
}
