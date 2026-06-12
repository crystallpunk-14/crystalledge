using Content.Shared._CE.Currency;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityTable.EntitySelectors;

/// <summary>
/// Spawns a random amount of currency between <see cref="Min"/> and <see cref="Max"/>.
/// Use <see cref="LargeCoinChance"/> to control denomination spread:
/// 1.0 = fewest coins possible (optimal), 0.0–0.5 = many smaller coins (good for treasure chests).
/// </summary>
public sealed partial class CEMoneySelector : EntityTableSelector
{
    [DataField(required: true)]
    public int Min;

    [DataField(required: true)]
    public int Max;

    /// <summary>
    /// Per-group probability of upgrading 10 coins into one coin of the next denomination.
    /// 1.0 = greedy/optimal (use for change). 0.3–0.5 = varied smaller coins (use for loot).
    /// </summary>
    [DataField]
    public float LargeCoinChance = 1.0f;

    protected override IEnumerable<EntProtoId> GetSpawnsImplementation(System.Random rand,
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var amount = rand.Next(Min, Max + 1);
        return CECurrencySystem.GetCoinProtos(amount, LargeCoinChance, rand);
    }

    protected override IEnumerable<(EntProtoId spawn, double)> ListSpawnsImplementation(
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var avg = (Min + Max) / 2;
        var rand = new System.Random(42); // stable seed — analytics only
        var seen = new HashSet<EntProtoId>();
        foreach (var p in CECurrencySystem.GetCoinProtos(avg, LargeCoinChance, rand))
            if (seen.Add(p)) yield return (p, 1.0);
    }

    protected override IEnumerable<(EntProtoId spawn, double)> AverageSpawnsImplementation(
        IEntityManager entMan,
        IPrototypeManager proto,
        EntityTableContext ctx)
    {
        var avg = (Min + Max) / 2;
        var rand = new System.Random(42);
        var counts = new Dictionary<EntProtoId, int>();
        foreach (var p in CECurrencySystem.GetCoinProtos(avg, LargeCoinChance, rand))
        {
            counts.TryGetValue(p, out var c);
            counts[p] = c + 1;
        }
        foreach (var (id, count) in counts)
            yield return (id, count);
    }
}
