using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace SpireRange;

// Each temporary encounter owns its target; no shared monster is mutated.
public sealed class RangeEncounter : EncounterModel
{
    public MonsterModel? Target { get; set; }
    public override RoomType RoomType => RoomType.Monster;
    public override bool ShouldGiveRewards => false;
    public override bool IsDebugEncounter => true;
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        Target is null ? Array.Empty<MonsterModel>() : new[] { Target };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        new[] { ((Target ?? throw new InvalidOperationException("No target selected.")).ToMutable(), (string?)null) };
}
