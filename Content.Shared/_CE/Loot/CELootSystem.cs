using Content.Shared._CE.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Loot;

public sealed partial class CELootSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public List<(EntProtoId Id, float Weight)> BuildPool(IEnumerable<ProtoId<CETagPrototype>> tags)
    {
        var tagSet = tags as HashSet<ProtoId<CETagPrototype>> ?? new HashSet<ProtoId<CETagPrototype>>(tags);
        var pool = new List<(EntProtoId, float)>();
        foreach (var entProto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (entProto.Abstract)
                continue;
            if (!entProto.Components.TryGetValue("CELootCategory", out var entry))
                continue;
            if (entry.Component is not CELootCategoryComponent cat)
                continue;
            var weight = 0f;
            foreach (var e in cat.Tags)
            {
                if (tagSet.Contains(e.Tag))
                    weight += e.Weight;
            }

            if (weight > 0f)
                pool.Add((new EntProtoId(entProto.ID), weight));
        }

        return pool;
    }

    public EntProtoId? PickRandom(IEnumerable<ProtoId<CETagPrototype>> tags, System.Random rand)
    {
        return PickFromPool(BuildPool(tags), () => (float)rand.NextDouble());
    }

    public EntProtoId? PickRandom(IEnumerable<ProtoId<CETagPrototype>> tags, IRobustRandom rand)
    {
        return PickFromPool(BuildPool(tags), rand.NextFloat);
    }

    private static EntProtoId? PickFromPool(List<(EntProtoId Id, float Weight)> pool, Func<float> nextFloat)
    {
        if (pool.Count == 0)
            return null;
        var total = 0f;
        foreach (var (_, w) in pool)
        {
            total += w;
        }

        var roll = nextFloat() * total;
        var cum = 0f;
        var picked = pool[^1].Id;
        foreach (var (id, w) in pool)
        {
            cum += w;
            if (roll <= cum)
            {
                picked = id;
                break;
            }
        }

        return picked;
    }
}
