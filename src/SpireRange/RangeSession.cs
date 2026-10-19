using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace SpireRange;

internal sealed record RangeConfig(CharacterModel Character, CardModel Card, MonsterModel Monster,
    bool Upgraded, int Energy, string Seed);

internal static class RangeSession
{
    public static bool IsActive { get; private set; }
    public static bool IsBusy { get; private set; }
    public static RangeConfig? Config { get; private set; }
    private static RunState? _state;

    public static async Task Start(RangeConfig config)
    {
        if (IsBusy) return;
        if (!IsActive && RunManager.Instance.IsInProgress)
            throw new InvalidOperationException("请先返回主菜单再进入靶场。");
        if (IsActive) EnsureIdle();
        IsBusy = true;
        try
        {
            if (IsActive) await Stop();
            Config = config;
            IsActive = true;
            var player = Player.CreateForNewRun(config.Character, UnlockState.all, 1UL);
            _state = RunState.CreateForNewRun(new[] { player },
                ActModel.GetDefaultList().Select(a => a.ToMutable()).ToArray(),
                Array.Empty<ModifierModel>(), GameMode.Standard, 0, config.Seed);
            var manager = RunManager.Instance;
            manager.SetUpNewSingleplayer(_state, shouldSave: false);
            manager.CombatReplayWriter.IsEnabled = false;

            // A small deck guarantees the test card is drawn through the normal turn setup.
            player.Deck.Clear(silent: true);
            for (var i = 0; i < 5; i++)
            {
                var card = _state.CreateCard(config.Card, player);
                if (config.Upgraded && card.IsUpgradable) CardCmd.Upgrade(card);
                player.Deck.AddInternal(card, silent: true);
            }
            player.MaxEnergy = config.Energy;
            await PreloadManager.LoadRunAssets(new[] { config.Character });
            await PreloadManager.LoadActAssets(_state.Act);
            await manager.FinalizeStartingRelics();
            manager.Launch();
            var game = NGame.Instance ?? throw new InvalidOperationException("Game scene is not ready.");
            game.RootSceneContainer.SetCurrentScene(NRun.Create(_state));
            await manager.SetActInternal(0);

            ModelDb.Inject(typeof(RangeEncounter));
            var encounter = (RangeEncounter)ModelDb.Encounter<RangeEncounter>().ToMutable();
            encounter.Target = config.Monster;
            await manager.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, encounter);
            GD.Print($"[SpireRange] Started: {config.Character.Id} / {config.Card.Id} / {config.Monster.Id}");
        }
        catch
        {
            if (IsActive) await Stop();
            throw;
        }
        finally { IsBusy = false; }
    }

    public static async Task AddCard()
    {
        if (!IsActive || IsBusy || Config is null || _state is null) return;
        if (!CombatManager.Instance.IsInProgress)
            throw new InvalidOperationException("当前战斗已结束，请重置靶场。");
        IsBusy = true;
        try
        {
            var player = _state.Players[0];
            if (PileType.Hand.GetPile(player).Cards.Count >= CardPile.MaxCardsInHand)
                throw new InvalidOperationException("手牌已满。");
            var combat = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is not ready.");
            if (CombatManager.Instance.PlayerActionsDisabled || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
                throw new InvalidOperationException("请等待玩家可出牌时再添加卡牌。");
            if (RunManager.Instance.ActionExecutor.IsRunning)
                throw new InvalidOperationException("请等待当前出牌效果执行完毕。");
            var card = combat.CreateCard(Config.Card, player);
            if (Config.Upgraded && card.IsUpgradable) CardCmd.Upgrade(card);
            await CardPileCmd.Add(card, PileType.Hand);
        }
        finally { IsBusy = false; }
    }

    public static async Task Exit()
    {
        if (!IsActive || IsBusy) return;
        EnsureIdle();
        IsBusy = true;
        try { await Stop(); }
        finally { IsBusy = false; }
    }

    public static void OnMainMenuReached()
    {
        if (!RunManager.Instance.IsInProgress && !IsBusy)
        {
            IsActive = false;
            _state = null;
        }
    }

    private static void EnsureIdle()
    {
        if (CombatManager.Instance.IsStarting || CombatManager.Instance.IsEnding ||
            RunManager.Instance.ActionExecutor.IsRunning)
            throw new InvalidOperationException("请等待当前战斗动画或卡牌效果执行完毕后再操作。");
    }

    private static async Task Stop()
    {
        try
        {
            RunManager.Instance.CleanUp(graceful: false);
            await PreloadManager.LoadMainMenuEssentials();
            NGame.Instance?.RootSceneContainer.SetCurrentScene(NMainMenu.Create(openTimeline: false));
        }
        finally
        {
            IsActive = false;
            _state = null;
        }
    }
}
