using Content.Shared._CE.Frost;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Fire;

public sealed class CEFireSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId _defaultFireProto = "CEFireTileLow";

    private readonly EntProtoId _fireImpactEffect = "CEFireImpactEffect";
    private readonly EntProtoId _steamEffect = "CESteamEffect";
    private readonly SoundSpecifier _fireSound = new SoundPathSpecifier("/Audio/_CE/Effects/fire_whoosh.ogg");
    private readonly SoundSpecifier _steamSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    private EntityQuery<CEFireComponent> _fireQuery;
    private EntityQuery<CEFlammableComponent> _flammableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();
        _flammableQuery = GetEntityQuery<CEFlammableComponent>();

        SubscribeLocalEvent<CEFireComponent, MapInitEvent>(OnFireMapInit);
        SubscribeLocalEvent<CEFireComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<CEMeltTransformComponent, CEIgnitedEvent>(OnMeltingIgnited);
        SubscribeLocalEvent<CEFlammableComponent, CEIgnitedEvent>(OnFlammableIgnited);
        SubscribeLocalEvent<CEFlammableComponent, CEFreezeEntityAttemptEvent>(OnFreezeEntityAttempt);

        SubscribeLocalEvent<CEFlammableComponent, MapInitEvent>(OnMapInit);

        // Tile attempt: ice/melt-transform entities block fire tile placement.
        SubscribeLocalEvent<CEIgniteTileAttemptEvent>(OnIgniteTileAttempt);
    }

    private void OnMapInit(Entity<CEFlammableComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var dur = ent.Comp.BurnCycleDuration.TotalSeconds;
        ent.Comp.BurnCycleDuration = TimeSpan.FromSeconds(_random.NextDouble(dur * 0.75, dur * 1.25));
        Dirty(ent);
    }

    private void OnFlammableIgnited(Entity<CEFlammableComponent> ent, ref CEIgnitedEvent args)
    {
        if (_net.IsClient)
            return;

        var stacks = args.Stacks;
        var cycleDuration = ent.Comp.BurnCycleDuration;

        if (args.MaxStacks != null)
        {
            var current = _stack.GetStack(ent, ent.Comp.StatusEffect);
            var allowed = Math.Max(0, args.MaxStacks.Value - current);
            if (allowed <= 0)
                return;

            stacks = Math.Min(stacks, allowed);
        }

        _stack.TryAddStack(ent, ent.Comp.StatusEffect, stacks, cycleDuration);
        _stack.SetStackDelta(ent, ent.Comp.StatusEffect, ent.Comp.StackDelta);
    }

    /// <summary>
    /// Fire neutralizes frost: when something tries to freeze a burning entity,
    /// fire stacks cancel out an equal number of incoming frost stacks.
    /// </summary>
    private void OnFreezeEntityAttempt(Entity<CEFlammableComponent> ent, ref CEFreezeEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var fireStacks = _stack.GetStack(ent, ent.Comp.StatusEffect);
        if (fireStacks <= 0)
            return;

        var neutralized = Math.Min(fireStacks, args.Stacks);
        _stack.TryRemoveStack(ent, ent.Comp.StatusEffect, neutralized);
        args.Stacks -= neutralized;

        SpawnSteamEffect(ent);

        if (args.Stacks <= 0)
            args.Cancelled = true;
    }

    /// <summary>
    /// When fire is placed on a tile with melt-transform entities (e.g. ice),
    /// the entity transforms and the fire tile placement is cancelled.
    /// </summary>
    private void OnIgniteTileAttempt(ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_mapManager.TryFindGridAt(args.Coordinates, out var gridUid, out var grid))
            return;

        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), args.Coordinates);

        foreach (var ent in anchored)
        {
            var effectEv = new CEIgnitedEvent();
            RaiseLocalEvent(ent, ref effectEv);
            if (effectEv.Handled)
            {
                args.Cancelled = true;
                SpawnSteamEffect(args.Coordinates);
                return;
            }
        }
    }

    private void OnMeltingIgnited(Entity<CEMeltTransformComponent> ent, ref CEIgnitedEvent args)
    {
        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var coordinates = _transform.GetMapCoordinates(ent, xform);

        _entManager.DeleteEntity(ent);

        var restored = _entManager.SpawnEntity(ent.Comp.MeltsInto, coordinates);
        _transform.SetLocalRotation(restored, rotation);

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CEFireComponent>();
        while (query.MoveNext(out var uid, out var fire))
        {
            if (_timing.CurTime < fire.NextDecayTime)
                continue;

            fire.NextDecayTime = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat(fire.MinDecayInterval, fire.MaxDecayInterval));

            AddStacks((uid, fire), -1);
        }
    }

    private void OnCollide(Entity<CEFireComponent> ent, ref StartCollideEvent args)
    {
        IgniteEntity(args.OtherEntity, ent, ent.Comp.Stacks, ent.Comp.Stacks);
    }

    private void OnFireMapInit(Entity<CEFireComponent> ent, ref MapInitEvent args)
    {
        // Set initial decay time.
        ent.Comp.NextDecayTime = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat(ent.Comp.MinDecayInterval, ent.Comp.MaxDecayInterval));

        // Update appearance for initial stacks.
        UpdateAppearance(ent);

        // Element interaction: check for opposing element on the tile.
        var coords = _transform.GetMapCoordinates(ent);
        var attemptEv = new CEIgniteTileAttemptEvent(coords, ent.Comp.Stacks, false);
        RaiseLocalEvent(ref attemptEv);
        if (attemptEv.Cancelled)
        {
            EntityManager.DeleteEntity(ent);
            return;
        }

        // Ignite entities already on the tile.
        var entitiesOnTile = _lookup.GetEntitiesInRange(coords, 0.5f, LookupFlags.Uncontained);
        foreach (var entity in entitiesOnTile)
        {
            IgniteEntity(entity, ent, ent.Comp.Stacks, ent.Comp.Stacks);
        }
    }

    /// <summary>
    /// Adds or removes stacks from a fire tile. Handles appearance updates and deletion at 0 stacks.
    /// </summary>
    public void AddStacks(Entity<CEFireComponent> ent, int delta)
    {
        if (delta == 0)
            return;

        var oldStacks = ent.Comp.Stacks;
        ent.Comp.Stacks = Math.Max(0, ent.Comp.Stacks + delta);
        Dirty(ent);

        if (ent.Comp.Stacks <= 0)
        {
            EntityManager.DeleteEntity(ent);
            return;
        }

        if (oldStacks != ent.Comp.Stacks)
            UpdateAppearance(ent);
    }

    /// <summary>
    /// Sets the fire tile to a specific stack count. Handles appearance updates and deletion at 0 stacks.
    /// </summary>
    public void SetStacks(Entity<CEFireComponent> ent, int stacks)
    {
        if (ent.Comp.Stacks == stacks)
            return;

        ent.Comp.Stacks = Math.Max(0, stacks);
        Dirty(ent);

        if (ent.Comp.Stacks <= 0)
        {
            EntityManager.DeleteEntity(ent);
            return;
        }

        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<CEFireComponent> ent)
    {
        var level = CEFireTileVisualLevel.Low;
        if (ent.Comp.Stacks >= ent.Comp.MediumThreshold)
            level = CEFireTileVisualLevel.Medium;
        if (ent.Comp.Stacks >= ent.Comp.HighThreshold)
            level = CEFireTileVisualLevel.High;

        _appearance.SetData(ent, CEFireTileVisuals.Level, level);
    }

    /// <summary>
    /// Raises a <see cref="CEIgnitedEvent"/> on the target entity.
    /// Entities with fire-related components handle the event to apply their effects.
    /// </summary>
    public void IgniteEntity(EntityUid target, EntityUid? source = null, int stack = 1, int? maxStack = null)
    {
        if (stack <= 0)
            return;

        if (_net.IsClient)
            return;

        // Element interaction: fire vs frost mutual neutralization.
        var attemptEv = new CEIgniteEntityAttemptEvent(target, stack, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stack = attemptEv.Stacks;

        var ignitedEv = new CEIgnitedEvent(stack, maxStack);
        RaiseLocalEvent(target, ref ignitedEv);
    }

    /// <summary>
    /// Creates or adds stacks to fire on the tile and ignites all entities on the tile.
    /// </summary>
    public void IgniteTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates, int stacks = 1)
    {
        if (_net.IsClient)
            return;

        if (stacks <= 0)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        var attemptEv = new CEIgniteTileAttemptEvent(coordinates, stacks, false);
        RaiseLocalEvent(ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stacks = attemptEv.Stacks;

        // Spawn or add stacks to fire tile entity.
        var existingFires = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        var fireExists = false;

        foreach (var fire in existingFires)
        {
            if (_fireQuery.TryComp(fire, out var existingComp))
            {
                AddStacks((fire, existingComp), stacks);
                fireExists = true;
                break;
            }
        }

        if (!fireExists)
        {
            var newFire = _entManager.SpawnEntity(_defaultFireProto, coordinates);
            if (_fireQuery.TryComp(newFire, out var newComp))
                SetStacks((newFire, newComp), stacks);

            var fx = _entManager.SpawnEntity(_fireImpactEffect, coordinates);
            _audio.PlayPvs(_fireSound, fx);
        }

        // Ignite all entities on the tile.
        var entities = _lookup.GetEntitiesInRange(coordinates, 0.5f, LookupFlags.Uncontained);
        foreach (var ent in entities)
        {
            IgniteEntity(ent, null, stacks, stacks);
        }
    }

    public void IgniteArea(EntityCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 10)
    {
        var mapCoords = _transform.ToMapCoordinates(center);
        IgniteArea(mapCoords, radius, falloffFactor, maxStacks);
    }

    public void IgniteArea(MapCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 10)
    {
        if (radius <= 0f)
            return;

        if (!_mapManager.TryFindGridAt(center, out var gridUid, out var grid))
            return;

        var centerWorld = center.Position;
        var tileSize = grid.TileSize;

        var minX = (int)MathF.Floor((centerWorld.X - radius) / tileSize);
        var maxX = (int)MathF.Ceiling((centerWorld.X + radius) / tileSize);
        var minY = (int)MathF.Floor((centerWorld.Y - radius) / tileSize);
        var maxY = (int)MathF.Ceiling((centerWorld.Y + radius) / tileSize);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var tileIndices = new Vector2i(x, y);
                var tileWorldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, tileIndices);
                var tileCoords = new MapCoordinates(tileWorldPos, center.MapId);

                var distance = (tileWorldPos - centerWorld).Length();

                if (distance > radius)
                    continue;

                if (!_examine.InRangeUnOccluded(center, tileCoords, radius, null))
                    continue;

                var normalizedDistance = distance / radius;
                var stacks = CalculateFireStacks(normalizedDistance, falloffFactor, maxStacks);

                IgniteTile((gridUid, grid), tileCoords, stacks);
            }
        }
    }

    private int CalculateFireStacks(float normalizedDistance, float falloffFactor, int maxStacks)
    {
        var adjustedDistance = MathF.Pow(normalizedDistance, falloffFactor);
        var intensity = 1f - adjustedDistance;
        return Math.Max(1, (int)MathF.Ceiling(intensity * maxStacks));
    }

    /// <summary>
    /// Removes all CE fire stacks from an entity and spawns a steam effect.
    /// </summary>
    /// <returns>True if the entity had fire and was extinguished.</returns>
    public bool ExtinguishEntity(Entity<CEFlammableComponent?> target)
    {
        if (_net.IsClient)
            return false;

        if (!_flammableQuery.Resolve(target, ref target.Comp, logMissing: false))
            return false;

        var stacks = _stack.GetStack(target, target.Comp.StatusEffect);
        if (stacks <= 0)
            return false;

        _stack.TryRemoveStack(target, target.Comp.StatusEffect, stacks);
        SpawnSteamEffect(target);
        return true;
    }

    /// <summary>
    /// Spawns a steam effect at an entity's position.
    /// </summary>
    public void SpawnSteamEffect(EntityUid target)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(target).Coordinates;
        Spawn(_steamEffect, pos);
        _audio.PlayPvs(_steamSound, pos);
    }

    /// <summary>
    /// Spawns a steam effect at map coordinates.
    /// </summary>
    public void SpawnSteamEffect(MapCoordinates coordinates)
    {
        if (_net.IsClient)
            return;

        var steam = _entManager.SpawnEntity(_steamEffect, coordinates);
        _audio.PlayPvs(_steamSound, Transform(steam).Coordinates);
    }
}

/// <summary>
/// Appearance visuals key for fire tile entities.
/// </summary>
[NetSerializable, Serializable]
public enum CEFireTileVisuals
{
    Level,
}

/// <summary>
/// Visual level of a fire tile, driven by stack thresholds.
/// </summary>
[NetSerializable, Serializable]
public enum CEFireTileVisualLevel
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Raised as a directed event on the target entity before fire stacks are applied.
/// Handlers can modify <see cref="Stacks"/> or set <see cref="Cancelled"/> to prevent ignition.
/// Handled by <c>CEFrostSystem</c> for frost neutralization and <c>CESharedWaterSystem</c> for water blocking.
/// </summary>
[ByRefEvent]
public record struct CEIgniteEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before fire is placed on a tile.
/// Handlers can modify <see cref="Stacks"/> or set <see cref="Cancelled"/> to prevent ignition.
/// Handled by <c>CEFireSystem</c> (ice melting) and <c>CEWaterSystem</c> (water blocking).
/// </summary>
[ByRefEvent]
public record struct CEIgniteTileAttemptEvent(MapCoordinates Coordinates, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a directed event on an entity when it receives a fire/ignite effect.
/// Carries the fire intensity for handlers to apply their specific effects.
/// </summary>
[ByRefEvent]
public record struct CEIgnitedEvent(int Stacks = 0, int? MaxStacks = null, bool Handled = false);
