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
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityQuery<CEGOAPKnowledgeCacheComponent> _cacheQuery = default!;

    protected override void Resolve(ref CEGOAPSelectorResolveEvent<CEGOAPSelectorRandomEnemy> ev)
    {
        if (!_cacheQuery.TryGetComponent(ev.Agent, out var cache) || cache.Enemies.Count == 0)
            return;

        var alive = GetFilteredEnemies(ev.Agent, cache.Enemies, ev.Selector.Conditions);
        if (alive.Count == 0)
            return;

        var chosen = _random.Pick(alive);
        ev.Entity = chosen;
        if (_xformQuery.TryGetComponent(chosen, out var xform))
            ev.Position = xform.Coordinates;
    }
}
