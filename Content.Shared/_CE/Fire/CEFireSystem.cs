using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
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

    private readonly EntProtoId _defaultFireProto = "CEFireTileLow";

    private readonly EntProtoId _statusFire = "CEStatusEffectFire";

    private EntityQuery<CEFireComponent> _fireQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();

        SubscribeLocalEvent<CEFireComponent, MapInitEvent>(OnFireMapInit);
        SubscribeLocalEvent<CEFireComponent, StartCollideEvent>(OnCollide);

        SubscribeLocalEvent<CEFlammableComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEFlammableComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var dur = ent.Comp.BurnCycleDuration.TotalSeconds;
        ent.Comp.BurnCycleDuration = TimeSpan.FromSeconds(_random.NextDouble(dur * 0.75, dur * 1.25));
        Dirty(ent);
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

        // Read flammable overrides from the target, if present.
        var cycleDuration = TimeSpan.FromSeconds(2f);
        int? stackDeltaOverride = null;

        if (TryComp<CEFlammableComponent>(target, out var flammable))
        {
            cycleDuration = flammable.BurnCycleDuration;
            stackDeltaOverride = flammable.StackDelta;
        }

        // If a maxStack is provided, ensure we don't exceed it.
        if (maxStack != null)
        {
            var current = _stack.GetStack(target, _statusFire);
            var allowed = Math.Max(0, maxStack.Value - current);
            if (allowed <= 0)
                return;

            var toAdd = Math.Min(stack, allowed);

            _stack.TryAddStack(target, _statusFire, toAdd, cycleDuration);
        }
        else
        {
            _stack.TryAddStack(target, _statusFire, stack, cycleDuration);
        }

        // Apply flammable overrides to the status effect instance.
        if (stackDeltaOverride != null)
            _stack.SetStackDelta(target, _statusFire, stackDeltaOverride.Value);
    }

    /// <summary>
    /// Creates fire on the tile or adds stacks to existing fire.
    /// </summary>
    public void IgniteTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates, int stacks = 1)
    {
        if (_net.IsClient)
            return;

        if (stacks <= 0)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        // Don't ignite empty tiles (space / no turf).
        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        // Element interaction: fire vs ice tile mutual neutralization.
        var attemptEv = new CEIgniteTileAttemptEvent(coordinates, stacks, false);
        RaiseLocalEvent(ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stacks = attemptEv.Stacks;

        var existingFires = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);

        foreach (var fire in existingFires)
        {
            if (_fireQuery.TryComp(fire, out var existingComp))
            {
                // Fire already exists on this tile — add stacks to it.
                AddStacks((fire, existingComp), stacks);
                return;
            }
        }

        // No existing fire — spawn a new one and set stacks.
        var newFire = _entManager.SpawnEntity(_defaultFireProto, coordinates);
        if (_fireQuery.TryComp(newFire, out var newComp))
        {
            // Set stacks directly (MapInit already set it to initial value, so we override).
            SetStacks((newFire, newComp), stacks);
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
/// For tile fire entity
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFireComponent : Component
{
    /// <summary>
    /// Current number of fire stacks on this tile. Can be infinite.
    /// At 0, the fire entity is deleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Stacks = 1;

    /// <summary>
    /// Minimum seconds between decay ticks (loses 1 stack per tick).
    /// </summary>
    [DataField]
    public float MinDecayInterval = 5f;

    /// <summary>
    /// Maximum seconds between decay ticks (loses 1 stack per tick).
    /// </summary>
    [DataField]
    public float MaxDecayInterval = 10f;

    /// <summary>
    /// Next time a decay tick should happen.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextDecayTime = TimeSpan.Zero;

    /// <summary>
    /// Stack threshold for medium visual appearance.
    /// </summary>
    [DataField]
    public int MediumThreshold = 5;

    /// <summary>
    /// Stack threshold for high visual appearance.
    /// </summary>
    [DataField]
    public int HighThreshold = 10;
}

/// <summary>
/// Raised as a broadcast event before fire stacks are applied to an entity.
/// Handlers can modify Stacks or set Cancelled to prevent ignition.
/// </summary>
[ByRefEvent]
public record struct CEIgniteEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before fire is placed on a tile.
/// Handlers can modify Stacks or set Cancelled to prevent ignition.
/// </summary>
[ByRefEvent]
public record struct CEIgniteTileAttemptEvent(MapCoordinates Coordinates, int Stacks, bool Cancelled);
