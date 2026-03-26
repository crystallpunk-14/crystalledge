using Content.Shared._CE.Health.Components;
using Content.Shared.EntityTable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Destroys entities via QueueDel when accumulated damage reaches <see cref="CEDestructibleComponent.DestroyThreshold"/>.
/// Works independently from <see cref="CEMobStateSystem"/>.
/// </summary>
public sealed class CEDestructibleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDestructibleComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<CEDestructibleComponent> ent, ref CEDamageChangedEvent args)
    {
        if (args.NewDamage < ent.Comp.DestroyThreshold)
            return;

        if (TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        var xform = Transform(ent);
        EntityCoordinates position;

        if (TryComp<MapGridComponent>(xform.GridUid, out var mapGrid))
            position = new EntityCoordinates(xform.GridUid.Value, _maps.LocalToGrid(xform.GridUid.Value, mapGrid, xform.Coordinates));
        else if (xform.MapUid != null)
            position = new EntityCoordinates(xform.MapUid.Value, _transform.GetWorldPosition(xform));
        else
            return;

        if (ent.Comp.DestroySound is not null)
            _audio.PlayPvs(ent.Comp.DestroySound, Transform(ent).Coordinates);

        // Server-side: spawn loot. TODO: prediction someway??
        if (_net.IsServer && ent.Comp.LootTable is not null)
        {
            var spawns = _entityTable.GetSpawns(ent.Comp.LootTable);
            foreach (var spawn in spawns)
            {
                var spawnedLoot = SpawnAtPosition(spawn, position);
                _transform.SetLocalRotation(spawnedLoot, _random.NextAngle());
                _throwing.TryThrow(
                    spawnedLoot,
                    _random.NextAngle().ToVec() * _random.NextFloat(0, 0.25f),
                    2f
                );
            }
        }

        var destructedEv = new CEDestructedEvent(position, args.Source);
        RaiseLocalEvent(ent.Owner, ref destructedEv);

        PredictedQueueDel(ent.Owner);
    }
}

/// <summary>
/// Raised on an entity just before it is destroyed by <see cref="CEDestructibleSystem"/>.
/// </summary>
[ByRefEvent]
public record struct CEDestructedEvent(EntityCoordinates Position, EntityUid? Source);
