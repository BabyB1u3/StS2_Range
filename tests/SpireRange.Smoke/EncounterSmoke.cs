using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class EncounterSmoke
{
    public static void Run(Assembly mod)
    {
        foreach (var type in new[] { typeof(PhrogParasiteElite), typeof(PhrogParasite), typeof(Wriggler) })
            ModelDb.Inject(type);
        var native = ModelDb.Encounter<PhrogParasiteElite>();
        var state = DispatchProxy.Create<IRunState, EncounterRunProxy>();
        var prepare = AccessTools.Method(mod.GetType("SpireRange.RangeEncounterFactory"), "Prepare");
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var encounter = (EncounterModel)prepare.Invoke(null,
                new object[] { native, ModelDb.Monster<PhrogParasite>(), state })!;
            if (encounter is not PhrogParasiteElite || encounter.RoomType != RoomType.Elite || !encounter.HasScene)
                throw new Exception("The parasite lost its native elite encounter or scene.");
            if (encounter.MonstersWithSlots.Count != 1 ||
                encounter.MonstersWithSlots[0].Item1 is not PhrogParasite ||
                encounter.MonstersWithSlots[0].Item2 != "phrog")
                throw new Exception("The parasite was not generated in its native slot.");
            for (var i = 0; i < 4; i++)
                if (!encounter.Slots.Contains(PhrogParasiteElite.GetWrigglerSlotName(i)))
                    throw new Exception("A second-phase Wriggler slot is missing.");
            if (!encounter.AllPossibleMonsters.Any(m => m is Wriggler) || native.HaveMonstersBeenGenerated)
                throw new Exception("Native summons or the canonical encounter were changed.");
        }
        Console.WriteLine("PASS: parasite practice retains its native elite scene, initial slot, and all four second-phase slots across resets.");

        MonsterMoveStateMachine Moves(Wriggler monster) => (MonsterMoveStateMachine)
            AccessTools.Method(typeof(Wriggler), "GenerateMoveStateMachine").Invoke(monster, null)!;
        var invalid = (Wriggler)ModelDb.Monster<Wriggler>().ToMutable();
        var invalidCreature = new Creature(invalid, CombatSide.Enemy, null);
        try
        {
            Moves(invalid).RollMove(Array.Empty<Creature>(), invalidCreature, new RunRngSet("WRIGGLER_TEST").MonsterAi);
            throw new Exception("The regression fixture no longer reproduces the native missing-slot failure.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "No valid next state found.") { }

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var encounter = (EncounterModel)prepare.Invoke(null, new object[] { native, ModelDb.Monster<Wriggler>(), state })!;
            var (monster, slot) = encounter.MonstersWithSlots.Single();
            if (encounter is not PhrogParasiteElite || slot != PhrogParasiteElite.GetWrigglerSlotName(0)
                || monster is not Wriggler { StartStunned: true } wriggler)
                throw new Exception("Standalone Wriggler lost its native spawn scene, slot, or initial state.");
            var creature = new Creature(wriggler, CombatSide.Enemy, slot);
            var moves = Moves(wriggler);
            var rng = new RunRngSet("WRIGGLER_TEST").MonsterAi;
            var first = moves.RollMove(Array.Empty<Creature>(), creature, rng);
            if (first.Id != "SPAWNED_MOVE") throw new Exception("Native Wriggler spawn turn was not preserved.");
            moves.OnMovePerformed(first);
            var second = moves.RollMove(Array.Empty<Creature>(), creature, rng);
            if (second.Id != "NASTY_BITE_MOVE") throw new Exception("Wriggler could not choose its first active move.");
            moves.OnMovePerformed(second);
            if (moves.RollMove(Array.Empty<Creature>(), creature, rng).Id != "WRIGGLE_MOVE")
                throw new Exception("Wriggler could not continue its native move cycle.");
        }
        if (ModelDb.Monster<Wriggler>().StartStunned || native.HaveMonstersBeenGenerated)
            throw new Exception("Standalone Wriggler setup mutated canonical game models.");
        Console.WriteLine("PASS: missing-slot Wriggler failure reproduced; prepared native spawn, attack and buff moves succeed across resets.");
    }
}

public class EncounterRunProxy : DispatchProxy
{
    private readonly RunRngSet _rng = new("ENCOUNTER_SMOKE");

    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "get_Rng" => _rng,
        "get_TotalFloor" => 1,
        _ => throw new NotSupportedException(method?.Name)
    };
}
