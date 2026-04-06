using Content.Shared._CE.Fire;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Water;

public abstract class CESharedWaterSystem : EntitySystem
{
    [Dependency] protected readonly CEFireSystem Fire = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId _waterImpactEffect = "CEWaterTileImpactEffect";
    private readonly SoundSpecifier _waterSplashSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    private EntityQuery<CEWaterComponent> _waterQuery;
    private EntityQuery<CEFireComponent> _fireQuery;

    public override void Initialize()
    {
        base.Initialize();

        _waterQuery = GetEntityQuery<CEWaterComponent>();
        _fireQuery = GetEntityQuery<CEFireComponent>();

        // Water blocks fire on tiles and on entities standing on water.
        SubscribeLocalEvent<TransformComponent, CEIgniteEntityAttemptEvent>(OnIgniteEntityAttempt);
        SubscribeLocalEvent<CEWaterComponent, CEIgniteTileAttemptEvent>(OnIgniteTileAttempt);

        SubscribeLocalEvent<CEWettableComponent, CEIgniteEntityAttemptEvent>(OnWetIgniteAttempt);
        SubscribeLocalEvent<CEWettableComponent, CEWettedEvent>(OnWettableWetted);
    }

    #region Fire Blocking (tile-level)

    private void OnIgniteEntityAttempt(Entity<TransformComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!IsOnWater(ent))
            return;

        args.Cancelled = true;
        Fire.SpawnSteamEffect(args.Target);
    }

    /// <summary>
    /// Water blocks fire tile placement. Water is NOT consumed.
    /// </summary>
    private void OnIgniteTileAttempt(Entity<CEWaterComponent> ent, ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        Fire.SpawnSteamEffect(args.Coordinates);
    }

    #endregion

    #region Wet Status Effect

    private void OnWettableWetted(Entity<CEWettableComponent> ent, ref CEWettedEvent args)
    {
        if (_net.IsClient)
            return;

        var stacks = args.Stacks;
        var cycleDuration = args.Duration ?? ent.Comp.DefaultDuration;

        if (args.MaxStacks != null)
        {
            var current = _stack.GetFlammableStack(ent, ent.Comp.StatusEffect);
            var allowed = Math.Max(0, args.MaxStacks.Value - current);
            if (allowed <= 0)
                return;

            stacks = Math.Min(stacks, allowed);
        }

        _stack.TryAddStack(ent, ent.Comp.StatusEffect, stacks, cycleDuration);
    }

    #endregion

    #region Mutual Exclusion

    /// <summary>
    /// Wet neutralizes fire: when something tries to ignite a wet entity,
    /// wet stacks cancel out an equal number of incoming fire stacks.
    /// </summary>
    private void OnWetIgniteAttempt(Entity<CEWettableComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var wetStacks = _stack.GetFlammableStack(ent, ent.Comp.StatusEffect);
        if (wetStacks <= 0)
            return;

        var neutralized = Math.Min(wetStacks, args.Stacks);
        _stack.TryRemoveStack(ent, ent.Comp.StatusEffect, neutralized);
        args.Stacks -= neutralized;

        Fire.SpawnSteamEffect(ent);

        if (args.Stacks <= 0)
            args.Cancelled = true;
    }

    #endregion

    #region API

    /// <summary>
    /// Raises a <see cref="CEWettedEvent"/> on the target entity.
    /// Entities with wet-related components handle the event to apply their effects.
    /// </summary>
    public void WetEntity(EntityUid target, int stack = 1, int? maxStack = null, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return;

        if (_net.IsClient)
            return;

        var attemptEv = new CEWetEntityAttemptEvent(target, stack, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stack = attemptEv.Stacks;

        var wettedEv = new CEWettedEvent(stack, maxStack, duration);
        RaiseLocalEvent(target, ref wettedEv);
    }

    /// <summary>
    /// Wets a single tile: extinguishes fire on the tile and applies wet stacks to all entities.
    /// </summary>
    public void WetTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates, int stacks = 1, int? maxStacks = null, TimeSpan? duration = null)
    {
        if (_net.IsClient)
            return;

        if (stacks <= 0)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        // Extinguish any fire tiles on this tile.
        var anchored = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        foreach (var ent in anchored)
        {
            if (_fireQuery.TryComp(ent, out var fireComp))
            {
                Fire.SpawnSteamEffect(coordinates);
                EntityManager.DeleteEntity(ent);
            }
        }

        // Wet all entities on the tile.
        var entities = _lookup.GetEntitiesInRange(coordinates, 0.5f, LookupFlags.Uncontained);
        foreach (var ent in entities)
        {
            Fire.ExtinguishEntity(new Entity<CEFlammableComponent?>(ent, null));
            WetEntity(ent, stacks, maxStacks, duration);
        }

        // Spawn water splash effect.
        var fx = _entManager.SpawnEntity(_waterImpactEffect, coordinates);
        _audio.PlayPvs(_waterSplashSound, fx);
    }

    /// <summary>
    /// Wets an area of tiles: extinguishes fire and applies wet stacks to all entities in range.
    /// </summary>
    public void WetArea(EntityCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 3)
    {
        var mapCoords = _transform.ToMapCoordinates(center);
        WetArea(mapCoords, radius, falloffFactor, maxStacks);
    }

    /// <summary>
    /// Wets an area of tiles: extinguishes fire and applies wet stacks to all entities in range.
    /// </summary>
    public void WetArea(MapCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 3)
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
                var stacks = (int)MathF.Ceiling((1f - MathF.Pow(normalizedDistance, falloffFactor)) * maxStacks);

                WetTile((gridUid, grid), tileCoords, Math.Max(1, stacks), stacks, null);
            }
        }
    }

    #endregion

    /// <summary>
    /// Checks if an entity is standing on a tile that contains a water entity.
    /// </summary>
    protected bool IsOnWater(Entity<TransformComponent> ent)
    {
        var xform = ent.Comp;
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var coords = _transform.GetMapCoordinates(ent);
        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), coords);

        foreach (var anchEnt in anchored)
        {
            if (_waterQuery.HasComp(anchEnt))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Raised as a directed event on the target entity before wet stacks are applied.
/// Handlers can modify <see cref="Stacks"/> or set <see cref="Cancelled"/> to prevent wetting.
/// Handled by <c>CEFlammableComponent</c> for fire neutralization.
/// </summary>
[ByRefEvent]
public record struct CEWetEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a directed event on an entity when it receives a water/wet effect.
/// Carries the wet intensity for handlers to apply their specific effects.
/// </summary>
[ByRefEvent]
public record struct CEWettedEvent(int Stacks = 0, int? MaxStacks = null, TimeSpan? Duration = null);

