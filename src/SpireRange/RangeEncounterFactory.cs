using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;

namespace SpireRange;

internal sealed record RangeEncounterSource(ActModel Act, int ActIndex, EncounterModel Encounter);

internal static class RangeEncounterFactory
{
    private static readonly FieldInfo MonstersWithSlots =
        AccessTools.Field(typeof(EncounterModel), "_monstersWithSlots")
        ?? throw new MissingFieldException(typeof(EncounterModel).FullName, "_monstersWithSlots");

    public static RangeEncounterSource? FindSource(MonsterModel? target) => target is null
        // This event encounter is absent from the acts' ordinary combat pools.
        ? new RangeEncounterSource(ActModel.GetDefaultList().First(), 0,
            ModelDb.Encounter<BattlewornDummyEventEncounter>())
        :
        ModelDb.ActsByIndex.SelectMany((acts, index) => acts.SelectMany(act => act.AllEncounters
                .Where(e => e.AllPossibleMonsters.Any(m => m.Id == target.Id))
                .Select(e => new RangeEncounterSource(act, index, e))))
            // Prefer encounters where the selected monster is the primary enemy,
            // rather than one that appears only as a summon in another encounter.
            .OrderBy(s => s.Encounter.AllPossibleMonsters.First().Id == target.Id ? 0 : 1)
            .ThenBy(s => s.Encounter.AllPossibleMonsters.Count())
            .ThenBy(s => s.Encounter.Id.Entry, StringComparer.Ordinal)
            .FirstOrDefault();

    public static EncounterModel Prepare(EncounterModel source, MonsterModel? target, IRunState state)
    {
        if (target is null)
        {
            // Match TrainingCamp's default: native event dummy, first HP tier.
            var dummy = (BattlewornDummyEventEncounter)ModelDb.Encounter<BattlewornDummyEventEncounter>().ToMutable();
            dummy.Setting = BattlewornDummyEventEncounter.DummySetting.Setting1;
            dummy.GenerateMonstersWithSlots(state);
            return dummy;
        }
        var encounter = source.ToMutable();
        encounter.GenerateMonstersWithSlots(state);
        var selected = encounter.MonstersWithSlots.FirstOrDefault(m => m.Item1.Id == target.Id);
        if (selected.Item1 is not null)
        {
            // Retain the native encounter's type, scene, slots, assets, hooks and room
            // category. Only its initial enemy list is reduced for the practice target.
            MonstersWithSlots.SetValue(encounter,
                new (MonsterModel, string?)[] { selected });
            return encounter;
        }

        if (encounter is PhrogParasiteElite && target is Wriggler)
        {
            // Native Wriggler INIT_MOVE branches on wriggler1..4. A generic
            // encounter has no slot, so none of its opening moves can be chosen.
            // Use the native scene and the same slot/spawn setup as InfestedPower.
            var wriggler = (Wriggler)target.ToMutable();
            wriggler.StartStunned = true;
            MonstersWithSlots.SetValue(encounter, new (MonsterModel, string?)[]
            {
                (wriggler, PhrogParasiteElite.GetWrigglerSlotName(0))
            });
            return encounter;
        }

        // Summon-only enemies may not have a native initial slot of their own.
        ModelDb.Inject(typeof(RangeEncounter));
        var fallback = (RangeEncounter)ModelDb.Encounter<RangeEncounter>().ToMutable();
        fallback.Target = target;
        fallback.RoomKind = source.RoomType;
        return fallback;
    }
}
