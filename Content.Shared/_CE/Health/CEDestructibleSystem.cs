using Content.Shared._CE.Health.Components;
using Content.Shared.EntityTable;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Destroys entities via QueueDel when accumulated damage reaches <see cref="CEDestructibleComponent.DestroyThreshold"/>.
/// For entities with <see cref="CEMobStateComponent"/>, the threshold is counted
/// from the moment they enter Critical.
/// </summary>
public sealed class CEDestructibleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Deferred destruction queue — processed in <see cref="Update"/> to avoid
    /// modifying entity archetype tables while other systems are enumerating queries
    /// (e.g. ThrownItemSystem).
    /// </summary>
    private readonly Queue<(EntityUid Uid, EntityUid? Source)> _pendingDestruction = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDestructibleComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pendingDestruction.TryDequeue(out var pending))
        {
            ProcessDestruction(pending.Uid, pending.Source);
        }
    }

    private void OnDamageChanged(Entity<CEDestructibleComponent> ent, ref CEDamageChangedEvent args)
    {
        var destroyThreshold = GetDestroyThreshold(ent, args.NewDamage);

        if (args.NewDamage < destroyThreshold)
            return;

        if (TerminatingOrDeleted(ent.Owner) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        _pendingDestruction.Enqueue((ent.Owner, args.Source));
    }

    private void ProcessDestruction(EntityUid uid, EntityUid? source)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;

        if (!TryComp<CEDestructibleComponent>(uid, out var comp))
            return;

        var xform = Transform(uid);
        EntityCoordinates position;

        if (TryComp<MapGridComponent>(xform.GridUid, out var mapGrid))
            position = new EntityCoordinates(xform.GridUid.Value, _maps.LocalToGrid(xform.GridUid.Value, mapGrid, xform.Coordinates));
        else if (xform.MapUid != null)
            position = new EntityCoordinates(xform.MapUid.Value, _transform.GetWorldPosition(xform));
        else
            return;

        if (comp.DestroySound is not null)
            _audio.PlayPredicted(comp.DestroySound, xform.Coordinates, source);

        if (_net.IsServer)
        {
            DropCarriedItems(uid, position);

            // Server-side: spawn loot. TODO: prediction someway??
            if (comp.LootTable is not null)
            {
                var spawns = _entityTable.GetSpawns(comp.LootTable);
                foreach (var spawn in spawns)
                {
                    var spawnedLoot = SpawnAtPosition(spawn, position);
                    ScatterDroppedItem(spawnedLoot, position);
                }
            }
        }

        var destructedEv = new CEDestructedEvent(position, source);
        RaiseLocalEvent(uid, ref destructedEv);

        PredictedQueueDel(uid);
    }

    private int GetDestroyThreshold(Entity<CEDestructibleComponent> ent, int totalDamage)
    {
        if (!TryComp<CEMobStateComponent>(ent, out var mobState))
            return ent.Comp.DestroyThreshold;

        if (totalDamage < mobState.CriticalThreshold)
            return int.MaxValue;

        return mobState.CriticalThreshold + ent.Comp.DestroyThreshold;
    }

    private void DropCarriedItems(EntityUid uid, EntityCoordinates position)
    {
        DropInventoryItems(uid, position);
        DropHandItems(uid, position);
    }

    private void DropInventoryItems(EntityUid uid, EntityCoordinates position)
    {
        if (!TryComp(uid, out InventoryComponent? inventory))
            return;

        var equippedItems = new List<(EntityUid Item, string Slot)>();
        var enumerator = _inventory.GetSlotEnumerator((uid, inventory));

        while (enumerator.NextItem(out var item, out var slot))
        {
            equippedItems.Add((item, slot.Name));
        }

        foreach (var (item, slot) in equippedItems)
        {
            if (TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
                continue;

            if (_inventory.TryUnequip(uid, uid, slot, out var removedItem, true, true, inventory: inventory))
            {
                ScatterDroppedItem(removedItem.Value, position);
                continue;
            }

            if (!_container.IsEntityInContainer(item))
                ScatterDroppedItem(item, position);
        }
    }

    private void DropHandItems(EntityUid uid, EntityCoordinates position)
    {
        if (!TryComp(uid, out HandsComponent? hands))
            return;

        var heldItems = new List<EntityUid>();
        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            heldItems.Add(held);
        }

        foreach (var held in heldItems)
        {
            if (TerminatingOrDeleted(held) || EntityManager.IsQueuedForDeletion(held))
                continue;

            _hands.TryDrop((uid, hands), held, checkActionBlocker: false, doDropInteraction: false);

            if (!_container.IsEntityInContainer(held))
                ScatterDroppedItem(held, position);
        }
    }

    private void ScatterDroppedItem(EntityUid item, EntityCoordinates position)
    {
        if (TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
            return;

        EmptyNestedStorage(item, position);

        _transform.SetLocalRotation(item, _random.NextAngle());
        _throwing.TryThrow(item, _random.NextAngle().ToVec() * _random.NextFloat(0, 0.25f), 2f);
    }

    private void EmptyNestedStorage(EntityUid item, EntityCoordinates position)
    {
        if (!TryComp(item, out StorageComponent? storage) || storage.StoredItems.Count == 0)
            return;

        var storedItems = new List<EntityUid>(storage.StoredItems.Keys);
        _container.EmptyContainer(storage.Container, destination: position);

        foreach (var stored in storedItems)
        {
            if (_container.IsEntityInContainer(stored))
                continue;

            ScatterDroppedItem(stored, position);
        }
    }
}

/// <summary>
/// Raised on an entity just before it is destroyed by <see cref="CEDestructibleSystem"/>.
/// </summary>
[ByRefEvent]
public record struct CEDestructedEvent(EntityCoordinates Position, EntityUid? Source);
