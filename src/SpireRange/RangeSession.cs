using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace SpireRange;

internal sealed record RangeConfig(CharacterModel Character, CardModel Card, MonsterModel Monster,
    bool Upgraded, int Energy, string Seed);

internal enum RangeBattleStatus { Preparing, Playing, Victory, Defeat }

internal static class RangeSession
{
    public static bool IsActive { get; private set; }
    public static bool IsBusy { get; private set; }
    public static RangeConfig? Config { get; private set; }
    public static RangeBattleStatus BattleStatus { get; private set; }
    private static RunState? _state;
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        CombatManager.Instance.CombatSetUp += combat =>
        {
            if (IsActive && ReferenceEquals(combat.RunState, _state))
                BattleStatus = RangeBattleStatus.Playing;
        };
        CombatManager.Instance.CombatWon += room =>
        {
            if (IsActive && ReferenceEquals(room.CombatState.RunState, _state))
                BattleStatus = RangeBattleStatus.Victory;
        };
        CombatManager.Instance.CombatEnded += room =>
        {
            if (IsActive && ReferenceEquals(room.CombatState.RunState, _state) &&
                BattleStatus != RangeBattleStatus.Victory)
                BattleStatus = RangeBattleStatus.Defeat;
        };
    }

    public static string? AddCardDisabledReason()
    {
        if (!IsActive) return "当前不在靶场。";
        if (BattleStatus == RangeBattleStatus.Victory) return "测试胜利，可重置或退出。";
        if (BattleStatus == RangeBattleStatus.Defeat) return "测试失败，可重置或退出。";
        if (IsBusy || BattleStatus == RangeBattleStatus.Preparing || CombatManager.Instance.IsStarting)
            return "正在准备战斗，请稍候。";
        if (_state is null || !CombatManager.Instance.IsInProgress || _state.IsGameOver)
            return "战斗已结束，可重置或退出。";
        var player = _state.Players[0];
        if (CombatManager.Instance.PlayerActionsDisabled || player.PlayerCombatState?.Phase != PlayerTurnPhase.Play)
            return "等待玩家出牌阶段。";
        if (RunManager.Instance.ActionExecutor.IsRunning) return "等待当前卡牌效果执行完毕。";
        if (PileType.Hand.GetPile(player).Cards.Count >= CardPile.MaxCardsInHand) return "手牌已满。";
        return null;
    }

    public static bool CanResetOrExit => IsActive && !IsBusy && !CombatManager.Instance.IsStarting &&
        !CombatManager.Instance.IsEnding && !RunManager.Instance.ActionExecutor.IsRunning;

    public static async Task Start(RangeConfig config)
    {
        Initialize();
        if (IsBusy) return;
        if (!IsActive && RunManager.Instance.IsInProgress)
            throw new InvalidOperationException("请先返回主菜单再进入靶场。");
        if (IsActive) EnsureIdle();
        IsBusy = true;
        try
        {
            if (IsActive) await Stop(returnToMenu: false);
            config = config with { Seed = RangeSeed.Resolve(config.Seed) };
            Config = config;
            IsActive = true;
            BattleStatus = RangeBattleStatus.Preparing;
            RangeLocalization.Register();
            var source = RangeEncounterFactory.FindSource(config.Monster)
                ?? throw new InvalidOperationException("未找到所选怪物的原版遭遇。");
            var acts = ActModel.GetDefaultList().ToList();
            acts[source.ActIndex] = source.Act;
            var player = Player.CreateForNewRun(config.Character, UnlockState.all, 1UL);
            _state = RunState.CreateForNewRun(new[] { player },
                acts.Select(a => a.ToMutable()).ToArray(),
                Array.Empty<ModifierModel>(), GameMode.Standard, 0, config.Seed);
            _state.CurrentActIndex = source.ActIndex;
            var manager = RunManager.Instance;
            manager.SetUpNewSingleplayer(_state, shouldSave: false);
            manager.CombatReplayWriter.IsEnabled = false;

            // Keep the character's starting inventory and add one test card to the deck.
            var card = _state.CreateCard(config.Card, player);
            if (config.Upgraded && card.IsUpgradable) CardCmd.Upgrade(card);
            player.Deck.AddInternal(card, silent: true);
            player.MaxEnergy = config.Energy;
            await PreloadManager.LoadRunAssets(new[] { config.Character });
            await PreloadManager.LoadActAssets(_state.Acts[source.ActIndex]);
            await manager.FinalizeStartingRelics();
            manager.Launch();
            var game = NGame.Instance ?? throw new InvalidOperationException("Game scene is not ready.");
            game.RootSceneContainer.SetCurrentScene(NRun.Create(_state));
            await manager.SetActInternal(source.ActIndex);

            var encounter = RangeEncounterFactory.Prepare(source.Encounter, config.Monster, _state);
            await manager.EnterRoomDebug(encounter.RoomType, MapPointType.Unassigned, encounter);
            GD.Print($"[SpireRange] Started: {config.Character.Id} / {config.Card.Id} / {config.Monster.Id} / seed={config.Seed}");
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
        // Stale clicks during a phase change are an ordinary unavailable action,
        // not an exception or a game error.
        if (Config is null || _state is null || AddCardDisabledReason() is not null) return;
        IsBusy = true;
        try
        {
            var player = _state.Players[0];
            var combat = CombatManager.Instance.DebugOnlyGetState()
                ?? throw new InvalidOperationException("Combat state is not ready.");
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

    private static async Task Stop(bool returnToMenu = true)
    {
        try
        {
            var game = NGame.Instance;
            if (returnToMenu && game is not null)
            {
                // The native flow loads the complete menu asset set before constructing
                // settings submenus. MainMenuEssentials alone is insufficient after death.
                await game.ReturnToMainMenu();
            }
            else
            {
                if (game is not null) await game.Transition.FadeOut();
                RunManager.Instance.CleanUp(graceful: true);
                if (game is not null)
                {
                    game.SetScreenShakeTarget(game);
                    game.RootSceneContainer.SetCurrentScene(new Control());
                    // Flush queued game-over/old-run nodes and their cancelled animations
                    // before loading assets and creating the replacement combat scene.
                    await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }
        finally
        {
            IsActive = false;
            _state = null;
        }
    }
}
