using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Fire;

public sealed class CEFireSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly EntProtoId _lowFire = "CEFireTileLow";
    private readonly EntProtoId _mediumFire = "CEFireTileMedium";
    private readonly EntProtoId _highFire = "CEFireTileHigh";

    private readonly EntProtoId _statusFire = "CEStatusEffectFire";

    private EntityQuery<CEFireComponent> _fireQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();

        SubscribeLocalEvent<CEFireComponent, MapInitEvent>(OnFireMapInit);
        SubscribeLocalEvent<CEFireComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<CEFireComponent> ent, ref StartCollideEvent args)
    {
        var stacks = ent.Comp.Intensity switch
        {
            FireIntensity.Low => 1,
            FireIntensity.Medium => 2,
            FireIntensity.High => 3,
            _ => 1
        };
        IgniteEntity(args.OtherEntity, ent, stacks, stacks);
    }

    private void OnFireMapInit(Entity<CEFireComponent> ent, ref MapInitEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent);
        var entitiesOnTile = _lookup.GetEntitiesInRange(coords, 0.5f);
        foreach (var entity in entitiesOnTile)
        {
            var stacks = ent.Comp.Intensity switch
            {
                FireIntensity.Low => 1,
                FireIntensity.Medium => 2,
                FireIntensity.High => 3,
                _ => 1
            };
            IgniteEntity(entity, ent, stacks, stacks);
        }
    }

    public void IgniteEntity(EntityUid target, EntityUid? source = null, int stack = 1, int? maxStack = null)
    {
        if (stack <= 0)
            return;

        if (_net.IsClient)
            return;

        // If a maxStack is provided, ensure we don't exceed it.
        if (maxStack != null)
        {
            var current = _stack.GetStack(target, _statusFire);
            var allowed = Math.Max(0, maxStack.Value - current);
            if (allowed <= 0)
                return;

            var toAdd = Math.Min(stack, allowed);

            _stack.TryAddStack(target, _statusFire, toAdd, TimeSpan.FromSeconds(10f));
            return;
        }

        _stack.TryAddStack(target, _statusFire, stack, TimeSpan.FromSeconds(10f));
    }

    /// <summary>
    /// We either create fire on the tile or intensify it.
    /// </summary>
    public void IgniteTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates, FireIntensity intensity = FireIntensity.Low)
    {
        if (_net.IsClient)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        // Don't ignite empty tiles (space / no turf)
        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        var existingFires = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        Entity<CEFireComponent>? existingFire = null;

        foreach (var fire in existingFires)
        {
            if (_fireQuery.TryComp(fire, out var existingComp))
            {
                existingFire = (fire, existingComp);
                break;
            }
        }

        var targetIntensity = intensity;
        if (existingFire != null)
        {
            targetIntensity = (FireIntensity)Math.Clamp((int)existingFire.Value.Comp.Intensity + (int)intensity,
                (int)FireIntensity.Low,
                (int)FireIntensity.High);

            EntityManager.DeleteEntity(existingFire.Value);
        }

        var firePrototype = targetIntensity switch
        {
            FireIntensity.Low => _lowFire,
            FireIntensity.Medium => _mediumFire,
            FireIntensity.High => _highFire,
            _ => _lowFire
        };
        _entManager.SpawnEntity(firePrototype, coordinates);
    }

    public void IgniteArea(EntityCoordinates center, float radius = 3f, float falloffFactor = 0.5f)
    {
        var mapCoords = _transform.ToMapCoordinates(center);
        IgniteArea(mapCoords, radius, falloffFactor);
    }

    public void IgniteArea(MapCoordinates center, float radius = 3f, float falloffFactor = 0.5f)
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
                var intensity = CalculateFireIntensity(normalizedDistance, falloffFactor);

                IgniteTile((gridUid, grid), tileCoords, intensity);
            }
        }
    }

    private FireIntensity CalculateFireIntensity(float normalizedDistance, float falloffFactor)
    {
        var adjustedDistance = MathF.Pow(normalizedDistance, falloffFactor);

        return adjustedDistance switch
        {
            <= 0.33f => FireIntensity.High,
            <= 0.66f => FireIntensity.Medium,
            _ => FireIntensity.Low
        };
    }
}

public enum FireIntensity : byte
{
    Low = 0,
    Medium = 1,
    High = 2
}

[RegisterComponent]
public sealed partial class CEFireComponent : Component
{
    [DataField(required: true)]
    public FireIntensity Intensity = FireIntensity.Low;
}
