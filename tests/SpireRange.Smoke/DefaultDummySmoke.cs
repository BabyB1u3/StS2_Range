using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class DefaultDummySmoke
{
    public static async Task Run(Assembly mod, Harmony harmony)
    {
        foreach (var type in typeof(ActModel).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ActModel).IsAssignableFrom(t)))
            ModelDb.Inject(type);
        foreach (var type in new[] { typeof(BattlewornDummyEventEncounter), typeof(BattleFriendV1), typeof(BattleFriendV2), typeof(BattleFriendV3) })
            ModelDb.Inject(type);
        var factory = mod.GetType("SpireRange.RangeEncounterFactory")!;
        var source = AccessTools.Method(factory, "FindSource").Invoke(null, new object?[] { null })!;
        var native = (EncounterModel)AccessTools.Property(source.GetType(), "Encounter").GetValue(source)!;
        if (native is not BattlewornDummyEventEncounter
            || (int)AccessTools.Property(source.GetType(), "ActIndex").GetValue(source)! != 0
            || !ReferenceEquals(AccessTools.Property(source.GetType(), "Act").GetValue(source), ActModel.GetDefaultList()[0]))
            throw new Exception("An empty monster selection did not use the native dummy in the default first act.");

        var session = mod.GetType("SpireRange.RangeSession")!;
        var active = AccessTools.Field(session, "<IsActive>k__BackingField");
        var state = AccessTools.Field(session, "_state");
        var config = AccessTools.Field(session, "<Config>k__BackingField");
        var oldActive = active.GetValue(null);
        var oldState = state.GetValue(null);
        var oldConfig = config.GetValue(null);
        object Config(MonsterModel? target) => Activator.CreateInstance(mod.GetType("SpireRange.RangeConfig")!,
            new object?[] { null, Array.CreateInstance(mod.GetType("SpireRange.RangeTestCard")!, 0), target, 3, "DUMMY_TEST" })!;
        var patch = mod.GetType("SpireRange.RangeDummyTimeLimitPatch")!;
        harmony.CreateClassProcessor(patch).Patch();
        var prefix = AccessTools.Method(patch, "Prefix");
        bool PassesThrough(BattleFriendV1 monster) => (bool)prefix.Invoke(null, new object?[] { monster, null })!;
        var proxy = DispatchProxy.Create<IRunState, EncounterRunProxy>();
        try
        {
            BattleFriendV1? previous = null;
            for (var reset = 0; reset < 3; reset++)
            {
                var encounter = (EncounterModel)AccessTools.Method(factory, "Prepare")
                    .Invoke(null, new object?[] { native, null, proxy })!;
                var (monster, slot) = encounter.MonstersWithSlots.Single();
                if (encounter is not BattlewornDummyEventEncounter { Setting: BattlewornDummyEventEncounter.DummySetting.Setting1 }
                    || encounter.RoomType != RoomType.Monster || encounter.ShouldGiveRewards
                    || monster is not BattleFriendV1 dummy || slot is not null
                    || dummy.MinInitialHp != 75 || dummy.MaxInitialHp != 75 || ReferenceEquals(dummy, previous))
                    throw new Exception("Default dummy lost its native setting, HP, room behavior or fresh reset instance.");
                previous = dummy;
                var ownedRun = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
                var combat = new CombatState(encounter, ownedRun);
                var creature = new Creature(dummy, CombatSide.Enemy, slot) { CombatState = combat };
                state.SetValue(null, ownedRun);
                config.SetValue(null, Config(null));
                active.SetValue(null, false);
                if (!PassesThrough(dummy)) throw new Exception("Normal event dummy lost its time limit.");
                active.SetValue(null, true);
                state.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(RunState)));
                if (!PassesThrough(dummy)) throw new Exception("An unrelated run's dummy was intercepted.");
                state.SetValue(null, ownedRun);
                config.SetValue(null, Config(ModelDb.Monster<PhrogParasite>()));
                if (!PassesThrough(dummy)) throw new Exception("An explicit monster selection was treated as the default.");
                config.SetValue(null, Config(null));
                // Execute the real patched hook. No time-limit power should be applied,
                // including during repeated starts where room initialization is async.
                await dummy.AfterAddedToRoom();
                if (creature.Powers.Any()) throw new Exception("Default dummy still received its event timer.");
                var moves = (MonsterMoveStateMachine)AccessTools.Method(typeof(BattleFriendV1), "GenerateMoveStateMachine")
                    .Invoke(dummy, null)!;
                var rng = new RunRngSet("DUMMY_TEST").MonsterAi;
                for (var turn = 0; turn < 6; turn++)
                {
                    var move = moves.RollMove(Array.Empty<Creature>(), creature, rng);
                    if (move.Id != "NOTHING_MOVE") throw new Exception("Default dummy no longer remains passive.");
                    moves.OnMovePerformed(move);
                }
            }
            if (native.HaveMonstersBeenGenerated || ((BattlewornDummyEventEncounter)native).Setting != BattlewornDummyEventEncounter.DummySetting.None)
                throw new Exception("Preparing the default dummy mutated the canonical event encounter.");
            Console.WriteLine("PASS: empty monster selection creates a fresh passive 75-HP native dummy; its timer is skipped only in the owned default-dummy combat.");
        }
        finally
        {
            active.SetValue(null, oldActive);
            state.SetValue(null, oldState);
            config.SetValue(null, oldConfig);
        }
    }
}
