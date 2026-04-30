using Content.Shared._CE.Health.Components;
using Content.Shared._CE.TileEffects.EffectTransform;
using Content.Shared.Examine;
using Content.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.TileEffects.Core;

public sealed partial class CETileEffectSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private EntityQuery<CETileEffectComponent> _tileQuery;

    public override void Initialize()
    {
        base.Initialize();

        _tileQuery = GetEntityQuery<CETileEffectComponent>();

        SubscribeLocalEvent<CETileEffectSpawnerComponent, MapInitEvent>(OnSpawnerInit);

        SubscribeLocalEvent<CETileEffectComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CETileEffectComponent, StartCollideEvent>(OnCollide);

        SubscribeLocalEvent<CEPreventTileEffectComponent, CEAttemptSpawnTileEffectEvent>(OnPreventTileEffect);
        SubscribeLocalEvent<CETileEffectNeutralizationComponent, CEAttemptReceiveTileEffectEvent>(OnTileNeutralize);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CETileEffectComponent>();
        while (query.MoveNext(out var uid, out var effect))
        {
            if (_timing.CurTime < effect.NextUpdate)
                continue;

            effect.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat(effect.MinDecayInterval, effect.MaxDecayInterval));

            TryAddStack((uid, effect), effect.StackDelta);

            if (TerminatingOrDeleted(uid))
                continue;

            // Affect entities on this tile each decay tick.
            RaiseAffectedByTileEffect((uid, effect));
        }
    }

    private void OnSpawnerInit(Entity<CETileEffectSpawnerComponent> ent, ref MapInitEvent args)
    {
        var coords = Transform(ent).Coordinates;
        if (TryApplyTileEffect(ent.Comp.TileEffect, ent, coords, ent.Comp.Amount, ent.Comp.Max))
            QueueDel(ent);
    }

    private void OnMapInit(Entity<CETileEffectComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat(ent.Comp.MinDecayInterval, ent.Comp.MaxDecayInterval));

        UpdateAppearance(ent, ent.Comp);
        Dirty(ent);

        if (_net.IsClient)
            return;

        // Affect entities already on this tile when the effect spawns.
        RaiseAffectedByTileEffect(ent);
    }

    private void OnCollide(Entity<CETileEffectComponent> ent, ref StartCollideEvent args)
    {
        if (_net.IsClient)
            return;

        var other = args.OtherEntity;
        var ev = new CEAffectedByTileEffectEvent(ent, other);
        RaiseLocalEvent(other, ref ev);
    }

    /// <summary>
    /// Adjusts the stack count of a tile effect entity by a delta. Deletes the entity when stacks reach zero.
    /// </summary>
    /// <param name="tile">The tile effect entity.</param>
    /// <param name="amount">Stack delta (positive to add, negative to remove).</param>
    /// <param name="max">Per-call stack cap (0 = use component's MaxStacks).</param>
    /// <returns>True if stacks were adjusted. False if the entity was deleted or the component was missing.</returns>
    public bool TryAddStack(Entity<CETileEffectComponent?> tile, int amount, int max = 0)
    {
        if (!_tileQuery.Resolve(tile, ref tile.Comp, logMissing: false))
            return false;

        if (amount == 0)
            return false;

        var newStacks = tile.Comp.Stacks + amount;

        var effectiveMax = tile.Comp.MaxStacks > 0 ? tile.Comp.MaxStacks : int.MaxValue;
        if (max > 0)
            effectiveMax = Math.Min(effectiveMax, max);

        newStacks = Math.Clamp(newStacks, 0, effectiveMax);

        if (newStacks <= 0)
        {
            Del(tile.Owner);
            return false;
        }

        SetStacks((tile.Owner, tile.Comp!), newStacks);
        return true;
    }

    /// <summary>
    /// Removes stacks from a tile effect entity. Deletes the entity when stacks reach zero.
    /// </summary>
    /// <param name="tile">The tile effect entity.</param>
    /// <param name="amount">Number of stacks to remove.</param>
    /// <returns>True if the operation completed. False if the component was missing.</returns>
    public bool TryRemoveStack(Entity<CETileEffectComponent?> tile, int amount = 1)
    {
        return TryAddStack(tile, -amount);
    }

    /// <summary>
    /// Returns the current stack count. Returns 0 if the component is missing.
    /// </summary>
    public int GetStacks(Entity<CETileEffectComponent?> tile)
    {
        if (!_tileQuery.Resolve(tile, ref tile.Comp, logMissing: false))
            return 0;

        return tile.Comp.Stacks;
    }

    /// <summary>
    /// Sets the stack count of a tile effect entity to an exact value. Deletes the entity when stacks reach zero.
    /// </summary>
    private void SetStacks(Entity<CETileEffectComponent> tile, int stacks, int max = 0)
    {
        var effectiveMax = tile.Comp.MaxStacks > 0 ? tile.Comp.MaxStacks : int.MaxValue;
        if (max > 0)
            effectiveMax = Math.Min(effectiveMax, max);

        var newStacks = Math.Clamp(stacks, 0, effectiveMax);

        if (newStacks <= 0)
        {
            Del(tile.Owner);
            return;
        }

        var oldStacks = tile.Comp.Stacks;
        tile.Comp.Stacks = newStacks;
        Dirty(tile);
        UpdateAppearance(tile.Owner, tile.Comp);

        var ev = new CETileEffectStackEditedEvent(tile, oldStacks, newStacks);
        RaiseLocalEvent(tile, ref ev);
    }

    /// <summary>
    /// Applies a tile effect to all tiles within <paramref name="radius"/> of <paramref name="center"/>,
    /// with LOS checking and distance-based stack falloff.
    /// </summary>
    /// <param name="tileEffect">Tile effect entity prototype to apply.</param>
    /// <param name="source">Optional source entity (used for attempt events).</param>
    /// <param name="center">World-space center of the effect.</param>
    /// <param name="radius">Radius in world units.</param>
    /// <param name="fallOffFactor">Falloff exponent; higher = steeper drop-off from center.</param>
    /// <param name="maxStacks">Maximum stacks to apply at the center tile.</param>
    /// <param name="checkLos">Whether to skip tiles not in line-of-sight of the center.</param>
    public void ApplyTileEffectArea(
        EntProtoId tileEffect,
        EntityUid? source,
        EntityCoordinates center,
        float radius = 3f,
        float fallOffFactor = 0.5f,
        int maxStacks = 10,
        bool checkLos = true)
    {
        if (_net.IsClient)
            return;

        if (radius <= 0f)
            return;

        var mapCenter = _transform.ToMapCoordinates(center);

        if (!_mapManager.TryFindGridAt(mapCenter, out var gridUid, out var grid))
            return;

        var centerWorld = mapCenter.Position;
        var tileSize = grid.TileSize;

        var minX = (int) MathF.Floor((centerWorld.X - radius) / tileSize);
        var maxX = (int) MathF.Ceiling((centerWorld.X + radius) / tileSize);
        var minY = (int) MathF.Floor((centerWorld.Y - radius) / tileSize);
        var maxY = (int) MathF.Ceiling((centerWorld.Y + radius) / tileSize);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var tileIndices = new Vector2i(x, y);
                var tileWorldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, tileIndices);
                var tileCoords = new MapCoordinates(tileWorldPos, mapCenter.MapId);

                var distance = (tileWorldPos - centerWorld).Length();

                if (distance > radius)
                    continue;

                if (checkLos && !_examine.InRangeUnOccluded(mapCenter, tileCoords, radius, null))
                    continue;

                var normalizedDistance = radius > 0f ? distance / radius : 0f;
                var adjustedDistance = MathF.Pow(normalizedDistance, fallOffFactor);
                var stacks = Math.Max(1, (int) MathF.Ceiling((1f - adjustedDistance) * maxStacks));

                TryApplyTileEffect(tileEffect, source, new EntityCoordinates(gridUid, tileWorldPos), stacks, maxStacks);
            }
        }
    }

    /// <summary>
    /// Attempts to spawn or add stacks to a tile effect entity at the given coordinates.
    /// </summary>
    /// <param name="tileEffect">Entity prototype to spawn. Must have <see cref="CETileEffectComponent"/>.</param>
    /// <param name="source">Optional source entity. Can cancel via <see cref="CEAttemptApplyTileEffectEvent"/>.</param>
    /// <param name="coords">Where to apply the tile effect.</param>
    /// <param name="amount">How many stacks to apply. Must be positive.</param>
    /// <param name="max">Per-call stack cap (0 = use the component's MaxStacks).</param>
    public bool TryApplyTileEffect(EntProtoId tileEffect, EntityUid? source, EntityCoordinates coords, int amount, int max = 0)
    {
        if (_net.IsClient)
            return false;

        if (amount <= 0)
            return false;

        // Validate that the prototype actually has CETileEffectComponent.
        if (!_protoManager.TryIndex<EntityPrototype>(tileEffect, out var proto))
            return false;

        if (!proto.HasComponent<CETileEffectComponent>(_compFactory))
            return false;

        // Allow the source entity to cancel the attempt.
        if (source is { } sourceUid)
        {
            var sourceAttempt = new CEAttemptApplyTileEffectEvent(tileEffect, amount);
            RaiseLocalEvent(sourceUid, ref sourceAttempt);
            if (sourceAttempt.Cancelled)
                return false;
        }

        var mapCoords = _transform.ToMapCoordinates(coords);

        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid))
            return false;

        if (!_mapSystem.TryGetTileRef(gridUid, grid, mapCoords.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        // Raise CEAttemptSpawnTileEffectEvent on each anchored entity so they can block the effect.
        var spawnAttempt = new CEAttemptSpawnTileEffectEvent(tileEffect, coords, amount);
        foreach (var anchEnt in _mapSystem.GetAnchoredEntities((gridUid, grid), mapCoords))
        {
            RaiseLocalEvent(anchEnt, ref spawnAttempt);
            if (spawnAttempt.Cancelled)
                return false;
        }

        // Allow existing tile effects to neutralize the incoming effect (e.g. freeze cancels fire).
        var receiveAttempt = new CEAttemptReceiveTileEffectEvent(tileEffect, amount);
        foreach (var anchEnt in _mapSystem.GetAnchoredEntities((gridUid, grid), mapCoords))
        {
            if (!_tileQuery.HasComp(anchEnt))
                continue;

            RaiseLocalEvent(anchEnt, ref receiveAttempt);

            if (receiveAttempt.Cancelled || receiveAttempt.RemainingAmount <= 0)
                return false;
        }

        amount = receiveAttempt.RemainingAmount;

        // Add stacks to an existing tile effect of the same prototype.
        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), mapCoords);
        foreach (var ent in anchored)
        {
            if (!_tileQuery.TryComp(ent, out var existing))
                continue;

            if (MetaData(ent).EntityPrototype?.ID != tileEffect.Id)
                continue;

            TryAddStack((ent, existing), amount, max);
            return true;
        }

        // Spawn a new tile effect entity.
        var spawned = Spawn(tileEffect, mapCoords);
        if (!_tileQuery.TryComp(spawned, out var comp))
            return false;

        if (source is { } applier)
            comp.Applier = applier;

        // Use SetStacks so the initial count equals `amount`, not the prototype default + amount.
        SetStacks((spawned, comp), amount, max);
        return true;
    }

    private void OnTileNeutralize(Entity<CETileEffectNeutralizationComponent> ent, ref CEAttemptReceiveTileEffectEvent args)
    {
        if (!ent.Comp.Neutralizes.Contains(args.TileEffect))
            return;

        if (!_tileQuery.TryComp(ent, out var tileComp))
            return;

        var coords = Transform(ent).Coordinates;

        var neutralized = Math.Min(tileComp.Stacks, args.RemainingAmount);
        TryRemoveStack((ent.Owner, tileComp), neutralized);
        args.RemainingAmount -= neutralized;

        if (ent.Comp.Vfx is { } vfx)
            Spawn(vfx, coords);

        if (ent.Comp.Sound is { } sound)
            _audio.PlayPvs(sound, coords);

        if (args.RemainingAmount <= 0)
            args.Cancelled = true;
    }

    private void OnPreventTileEffect(Entity<CEPreventTileEffectComponent> ent, ref CEAttemptSpawnTileEffectEvent args)
    {
        if (args.Cancelled)
            return;

        // Empty list = block all tile effects.
        if (ent.Comp.Blocks.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var blocked in ent.Comp.Blocks)
        {
            if (blocked.Id == args.TileEffect.Id)
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    /// <summary>
    /// Raises <see cref="CEAffectedByTileEffectEvent"/> on both the tile effect entity and each
    /// uncontained entity within 0.5 units of the tile effect (i.e., on the same tile).
    /// </summary>
    private void RaiseAffectedByTileEffect(Entity<CETileEffectComponent> tileEffect)
    {
        var coords = _transform.GetMapCoordinates(tileEffect);
        var entities = _lookup.GetEntitiesInRange(coords, 0.5f, LookupFlags.Uncontained);
        foreach (var entity in entities)
        {
            if (entity == tileEffect.Owner)
                continue;

            if (!HasComp<CEDamageableComponent>(entity) && !HasComp<CETileEffectComponent>(entity) && !HasComp<CETileEffectTransformComponent>(entity))
                continue;

            var ev = new CEAffectedByTileEffectEvent(tileEffect, entity);
            RaiseLocalEvent(entity, ref ev);
        }
    }

    private void UpdateAppearance(EntityUid uid, CETileEffectComponent comp)
    {
        var level = CETileEffectVisualLevel.Low;
        if (comp.Stacks >= comp.MediumThreshold)
            level = CETileEffectVisualLevel.Medium;
        if (comp.Stacks >= comp.HighThreshold)
            level = CETileEffectVisualLevel.High;

        _appearance.SetData(uid, CETileEffectVisuals.Level, level);
    }
}

/// <summary>
/// Appearance visuals key for tile effect entities.
/// </summary>
[NetSerializable, Serializable]
public enum CETileEffectVisuals
{
    Level,
}

/// <summary>
/// Visual intensity level for tile effect entities.
/// </summary>
[NetSerializable, Serializable]
public enum CETileEffectVisualLevel
{
    Low,
    Medium,
    High,
}


/// <summary>
/// Raised when an entity is touched or ticked by a tile effect.
/// </summary>
[ByRefEvent]
public record struct CEAffectedByTileEffectEvent(Entity<CETileEffectComponent> TileEffect, EntityUid AffectedEntity);

/// <summary>
/// Calls on effect entity, when a status effect stack is edited
/// </summary>
[ByRefEvent]
public readonly record struct CETileEffectStackEditedEvent(Entity<CETileEffectComponent> Target, int OldStack, int NewStack);

/// <summary>
/// Raised as a directed event on the source entity before a tile effect is applied.
/// Handlers can set <see cref="Cancelled"/> to prevent the tile effect from spawning/stacking.
/// </summary>
[ByRefEvent]
public record struct CEAttemptApplyTileEffectEvent(EntProtoId TileEffect, int Amount, bool Cancelled = false);

/// <summary>
/// Raised as a directed event on each anchored entity on the target tile before a tile effect is spawned.
/// Handlers can set <see cref="Cancelled"/> to block the spawn (e.g. <see cref="CEPreventTileEffectComponent"/> on water blocking fire).
/// </summary>
[ByRefEvent]
public record struct CEAttemptSpawnTileEffectEvent(EntProtoId TileEffect, EntityCoordinates Coordinates, int Amount, bool Cancelled = false);

/// <summary>
/// Raised as a directed event on each tile effect entity on the target tile before stacks are applied.
/// Handlers can reduce <see cref="RemainingAmount"/> (e.g. <see cref="CETileEffectNeutralizationComponent"/> on freeze absorbing fire stacks).
/// Setting <see cref="Cancelled"/> or reducing <see cref="RemainingAmount"/> to zero cancels the application entirely.
/// </summary>
[ByRefEvent]
public struct CEAttemptReceiveTileEffectEvent
{
    public readonly EntProtoId TileEffect;
    public int RemainingAmount;
    public bool Cancelled;

    public CEAttemptReceiveTileEffectEvent(EntProtoId tileEffect, int amount)
    {
        TileEffect = tileEffect;
        RemainingAmount = amount;
    }
}
