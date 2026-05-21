using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP.Selectors;
using Robust.Shared.Random;

namespace Content.Server._CE.GOAP.Selectors;

/// <summary>
/// Picks a random known enemy from <see cref="CEGOAPKnowledgeCacheComponent.Enemies"/>.
/// </summary>
public sealed partial class CEGOAPSelectorRandomEnemy : CEGOAPTargetSelectorBase<CEGOAPSelectorRandomEnemy>
{
}

public sealed partial class CEGOAPSelectorRandomEnemySystem : CEGOAPTargetSelectorSystem<CEGOAPSelectorRandomEnemy>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<CEGOAPKnowledgeCacheComponent> _cacheQuery = default!;

    protected override void Resolve(ref CEGOAPSelectorResolveEvent<CEGOAPSelectorRandomEnemy> ev)
    {
        if (!_cacheQuery.TryGetComponent(ev.Agent, out var cache) || cache.Enemies.Count == 0)
            return;

        var index = _random.Next(cache.Enemies.Count);
        var i = 0;
        foreach (var enemy in cache.Enemies)
        {
            if (i++ != index)
                continue;

            ev.Entity = enemy;
            if (_xformQuery.TryGetComponent(enemy, out var xform))
                ev.Position = xform.Coordinates;
            return;
        }
    }
}
