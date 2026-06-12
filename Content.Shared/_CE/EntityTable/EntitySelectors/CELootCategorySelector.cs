using Content.Shared._CE.Loot;
using Content.Shared._CE.Tag;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.EntityTable.ValueSelector;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityTable.EntitySelectors;

public sealed partial class CELootCategorySelector : EntityTableSelector
{
    [DataField(required: true)]
    public HashSet<ProtoId<CETagPrototype>> Tags = new();

    [DataField]
    public NumberSelector Amount = new ConstantNumberSelector(1);

    protected override IEnumerable<EntProtoId> GetSpawnsImplementation(
        System.Random rand,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var loot = entMan.System<CELootSystem>();
        var num = Amount.Get(rand);
        for (var i = 0; i < num; i++)
        {
            var picked = loot.PickRandom(Tags, rand);
            if (picked.HasValue)
                yield return picked.Value;
        }
    }

    protected override IEnumerable<(EntProtoId spawn, double)> ListSpawnsImplementation(
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var pool = entMan.System<CELootSystem>().BuildPool(Tags);
        if (pool.Count == 0)
            yield break;
        var total = 0.0;
        foreach (var (_, w) in pool)
        {
            total += w;
        }

        foreach (var (id, w) in pool)
        {
            yield return (id, w / total);
        }
    }

    // Amount draws per roll; expected count per entry = selection probability * average amount.
    protected override IEnumerable<(EntProtoId spawn, double)> AverageSpawnsImplementation(
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var avgAmount = Amount.Average();
        foreach (var (id, prob) in ListSpawnsImplementation(entMan, proto, ctx))
        {
            yield return (id, prob * avgAmount);
        }
    }
}
