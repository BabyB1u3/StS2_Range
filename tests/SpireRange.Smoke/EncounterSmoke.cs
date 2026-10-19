using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
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
