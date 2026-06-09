using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP.Selectors;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
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
    [Dependency] private CEMobStateSystem _mobState = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<CEGOAPKnowledgeCacheComponent> _cacheQuery = default!;
    [Dependency] private EntityQuery<CEMobStateComponent> _mobStateQuery = default!;

    protected override void Resolve(ref CEGOAPSelectorResolveEvent<CEGOAPSelectorRandomEnemy> ev)
    {
        if (!_cacheQuery.TryGetComponent(ev.Agent, out var cache) || cache.Enemies.Count == 0)
            return;

        var aliveEnemies = new List<EntityUid>();
        foreach (var enemy in cache.Enemies)
        {
            var isAlive = _mobStateQuery.TryGetComponent(enemy, out var mobState)
                ? _mobState.IsAlive(enemy, mobState)
                : !Terminating(enemy);
            if (isAlive)
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
