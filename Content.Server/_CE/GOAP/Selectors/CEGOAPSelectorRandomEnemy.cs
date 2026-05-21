using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.Health;
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
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<CEGOAPKnowledgeCacheComponent> _cacheQuery = default!;

    protected override void Resolve(ref CEGOAPSelectorResolveEvent<CEGOAPSelectorRandomEnemy> ev)
    {
        if (!_cacheQuery.TryGetComponent(ev.Agent, out var cache) || cache.Enemies.Count == 0)
            return;

        var aliveEnemies = new List<EntityUid>();
        foreach (var enemy in cache.Enemies)
        {
            if (_mobState.IsAlive(enemy))
                aliveEnemies.Add(enemy);
        }

        if (aliveEnemies.Count == 0)
            return;

        var chosen = _random.Pick(aliveEnemies);
        ev.Entity = chosen;
        if (_xformQuery.TryGetComponent(chosen, out var xform))
            ev.Position = xform.Coordinates;
    }
}
