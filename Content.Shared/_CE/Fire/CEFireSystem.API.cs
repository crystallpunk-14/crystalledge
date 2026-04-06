using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.Fire;

public sealed partial class CEFireSystem
{
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
        var anchored = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        foreach (var ent in anchored)
        {
            RaiseLocalEvent(ent, ref attemptEv);
            if (attemptEv.Cancelled)
                return;
        }
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
        }

        var fx = _entManager.SpawnEntity(_fireImpactEffect, coordinates);
        _audio.PlayPvs(_fireSound, fx);

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
}
