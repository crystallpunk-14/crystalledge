using Content.Shared.Examine;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects;

public sealed partial class CETileEffectSystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

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
}
