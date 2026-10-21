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

internal sealed record RangeTestCard(CardModel Card, bool Upgraded);

internal sealed record RangeConfig
{
    public CharacterModel Character { get; init; }
    public IReadOnlyList<RangeTestCard> Cards { get; }
    public MonsterModel Monster { get; init; }
    public int Energy { get; init; }
    public string Seed { get; init; }

    public RangeConfig(CharacterModel character, IEnumerable<RangeTestCard> cards, MonsterModel monster,
        int energy, string seed)
    {
        Character = character;
        Cards = Array.AsReadOnly(cards.ToArray());
        Monster = monster;
        Energy = energy;
        Seed = seed;
    }
}

internal enum RangeBattleStatus { Preparing, Playing, Victory, Defeat, Error }

internal static class RangeSession
{
    public static bool IsActive { get; private set; }
    public static bool IsBusy { get; private set; }
    public static RangeConfig? Config { get; private set; }
    public static RangeBattleStatus BattleStatus { get; private set; }
    public static string? StartupError { get; private set; }
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

    public static bool CanResetOrExit => IsActive && !IsBusy && (StartupError is not null ||
        (!CombatManager.Instance.IsStarting && !CombatManager.Instance.IsEnding && !RunManager.Instance.ActionExecutor.IsRunning));

    internal static async Task ObserveCombatStart(Task task, CombatState? combat)
    {
        var run = _state;
        var ownsCombat = IsActive && run is not null && ReferenceEquals(combat?.RunState, run);
        try { await task; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Only a completed, faulted startup task is recoverable here. A normal
            // pending startup must retain the usual animation/action guards.
            if (ownsCombat && IsActive && ReferenceEquals(_state, run))
            {
                StartupError = ex.GetBaseException().Message;
                BattleStatus = RangeBattleStatus.Error;
            }
            throw; // Keep the game's original error logging.
        }
    }

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
            StartupError = null;
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

            // Each entry is a separate copy, with its own upgrade state.
            AddTestCards(_state, player, config.Cards);
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
            GD.Print($"[SpireRange] Started: {config.Character.Id} / {config.Cards.Count} test cards / {config.Monster.Id} / seed={config.Seed}");
        }
        catch
        {
            if (IsActive) await Stop();
            throw;
        }
        finally { IsBusy = false; }
    }

    internal static void AddTestCards(ICardScope scope, Player player, IEnumerable<RangeTestCard> cards)
    {
        foreach (var selected in cards)
        {
            var card = scope.CreateCard(selected.Card, player);
            if (selected.Upgraded && card.IsUpgradable) CardCmd.Upgrade(card);
            player.Deck.AddInternal(card, silent: true);
        }
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
            StartupError = null;
        }
    }

    private static void EnsureIdle()
    {
        if (StartupError is not null) return;
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
            StartupError = null;
        }
    }
}
