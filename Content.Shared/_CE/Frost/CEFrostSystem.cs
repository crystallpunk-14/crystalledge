using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Frost;

public sealed class CEFrostSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId _defaultIceProto = "CEIce";
    private readonly EntProtoId _statusColdSlowdown = "CEStatusEffectColdSlowdown";
    private readonly EntProtoId _freezeEffect = "CEFreezeEffect";
    private readonly SoundSpecifier _freezeSound = new SoundPathSpecifier("/Audio/Items/Anomaly/ice_crit.ogg");

    private EntityQuery<CEIceComponent> _iceQuery;

    public override void Initialize()
    {
        base.Initialize();

        _iceQuery = GetEntityQuery<CEIceComponent>();

        SubscribeLocalEvent<CEIceComponent, MapInitEvent>(OnIceMapInit);

        SubscribeLocalEvent<CEFreezeImmunityStatusEffectComponent,
            StatusEffectRelayedEvent<CEFreezeEntityAttemptEvent>>(OnFreezeImmunity);
    }

    private void OnFreezeImmunity(Entity<CEFreezeImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEFreezeEntityAttemptEvent> args)
    {
        var inner = args.Args;
        inner.Cancelled = true;
        args.Args = inner;
    }

    private void OnIceMapInit(Entity<CEIceComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        // Element interaction: check for opposing element on the tile.
        var coords = _transform.GetMapCoordinates(ent);
        var attemptEv = new CEFreezeTileAttemptEvent(coords, false);
        RaiseLocalEvent(ref attemptEv);
        if (attemptEv.Cancelled)
        {
            _entManager.DeleteEntity(ent);
            return;
        }
    }

    /// <summary>
    /// Applies cold slowdown stacks to an entity.
    /// </summary>
    public void FreezeEntity(EntityUid target, int stack = 1, int? maxStack = null, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return;

        if (_net.IsClient)
            return;

        // Element interaction: frost vs fire mutual neutralization.
        var attemptEv = new CEFreezeEntityAttemptEvent(target, stack, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stack = attemptEv.Stacks;

        var cycleDuration = duration ?? TimeSpan.FromSeconds(5f);

        if (maxStack != null)
        {
            var current = _stack.GetStack(target, _statusColdSlowdown);
            var allowed = Math.Max(0, maxStack.Value - current);
            if (allowed <= 0)
                return;

            var toAdd = Math.Min(stack, allowed);
            _stack.TryAddStack(target, _statusColdSlowdown, toAdd, cycleDuration);
        }
        else
        {
            _stack.TryAddStack(target, _statusColdSlowdown, stack, cycleDuration);
        }
    }

    /// <summary>
    /// Spawns ice on the given tile if there is no ice already.
    /// </summary>
    public void FreezeTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates)
    {
        if (_net.IsClient)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        // Element interaction: ice vs fire tile mutual neutralization.
        var attemptEv = new CEFreezeTileAttemptEvent(coordinates, false);
        RaiseLocalEvent(ref attemptEv);
        if (attemptEv.Cancelled)
            return;

        // Check if ice already exists on this tile.
        var existing = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        foreach (var ent in existing)
        {
            if (_iceQuery.HasComp(ent))
                return;
        }

        _entManager.SpawnEntity(_defaultIceProto, coordinates);

        // Spawn freeze visual effect.
        var fx = _entManager.SpawnEntity(_freezeEffect, coordinates);
        _audio.PlayPvs(_freezeSound, fx);
    }

    /// <summary>
    /// Freezes an area: spawns ice on tiles and applies cold slowdown to entities in range.
    /// </summary>
    public void FreezeArea(EntityCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 3)
    {
        var mapCoords = _transform.ToMapCoordinates(center);
        FreezeArea(mapCoords, radius, falloffFactor, maxStacks);
    }

    /// <summary>
    /// Freezes an area: spawns ice on tiles and applies cold slowdown to entities in range.
    /// </summary>
    public void FreezeArea(MapCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 3)
    {
        if (radius <= 0f)
            return;

        if (!_mapManager.TryFindGridAt(center, out var gridUid, out var grid))
            return;

        var centerWorld = center.Position;
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
                var tileCoords = new MapCoordinates(tileWorldPos, center.MapId);

                var distance = (tileWorldPos - centerWorld).Length();

                if (distance > radius)
                    continue;

                if (!_examine.InRangeUnOccluded(center, tileCoords, radius, null))
                    continue;

                FreezeTile((gridUid, grid), tileCoords);
            }
        }

        // Apply cold slowdown to entities in the area.
        var entities = _lookup.GetEntitiesInRange(center, radius);
        foreach (var entity in entities)
        {
            var entPos = _transform.GetMapCoordinates(entity);
            var dist = (entPos.Position - centerWorld).Length();
            if (dist > radius)
                continue;

            var normalizedDist = dist / radius;
            var stacks = CalculateStacks(normalizedDist, falloffFactor, maxStacks);
            FreezeEntity(entity, stacks, maxStacks);
        }
    }

    private int CalculateStacks(float normalizedDistance, float falloffFactor, int maxStacks)
    {
        var adjustedDistance = MathF.Pow(normalizedDistance, falloffFactor);
        var intensity = 1f - adjustedDistance;
        return Math.Max(1, (int) MathF.Ceiling(intensity * maxStacks));
    }
}

/// <summary>
/// Raised as a broadcast event before frost/cold stacks are applied to an entity.
/// Handlers can modify Stacks or set Cancelled to prevent freezing.
/// </summary>
[ByRefEvent]
public record struct CEFreezeEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before ice is placed on a tile.
/// Handlers can set Cancelled to prevent freezing.
/// </summary>
[ByRefEvent]
public record struct CEFreezeTileAttemptEvent(MapCoordinates Coordinates, bool Cancelled);
