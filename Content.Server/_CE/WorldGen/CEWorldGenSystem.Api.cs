using System.Numerics;
using Content.Server._CE.WorldGen.Components;
using Content.Shared._CE.Maths;
using JetBrains.Annotations;

namespace Content.Server._CE.WorldGen;

/// <summary>
/// Public world-gen API and dev tooling, ported from the upstream biome system:
/// <list type="bullet">
///   <item>ReserveTiles — protect a region from generation/unload before placing structures.</item>
///   <item>Preload — force-generate chunks in an area before a player arrives.</item>
///   <item>Prototype hot-reload — regenerate loaded chunks when their chunk type is edited.</item>
/// </list>
/// </summary>
public sealed partial class CEWorldGenSystem
{
    /// <summary>
    /// Reserves every tile in a world-space box on the given z-level map: marks them modified so
    /// world-gen neither generates over them nor unloads them. Call before placing structures /
    /// prefabs into the world so the generator leaves them alone. Mirrors biome ReserveTiles.
    /// </summary>
    [PublicAPI]
    public void ReserveTiles(EntityUid mapUid, Box2 bounds)
    {
        if (!TryResolveWorldLevel(mapUid, out var world, out var chunkZ, out var level))
            return;

        var size = ChunkSize;
        var minX = (int)MathF.Floor(bounds.Left);
        var maxX = (int)MathF.Ceiling(bounds.Right);
        var minY = (int)MathF.Floor(bounds.Bottom);
        var maxY = (int)MathF.Ceiling(bounds.Top);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var cell = new Vector3i(FloorDiv(x, size), FloorDiv(y, size), chunkZ);
                MarkModified(GetOrCreateModified(world.Comp, cell), level, new Vector2i(x, y));
            }
        }
    }

    /// <summary>
    /// Force-generates every painted, not-yet-loaded chunk within <paramref name="radiusChunks"/> of a
    /// world position on the given z-level map, so content exists before a player arrives there.
    /// Preloaded chunks unload normally once no player keeps them in range. Mirrors biome Preload.
    /// </summary>
    [PublicAPI]
    public void Preload(EntityUid mapUid, Vector2 worldPos, int radiusChunks)
    {
        if (!TryResolveWorldLevel(mapUid, out var world, out var chunkZ, out _))
            return;

        var size = ChunkSize;
        var cx = (int)MathF.Floor(worldPos.X / size);
        var cy = (int)MathF.Floor(worldPos.Y / size);

        for (var dx = -radiusChunks; dx <= radiusChunks; dx++)
        {
            for (var dy = -radiusChunks; dy <= radiusChunks; dy++)
            {
                var cell = new Vector3i(cx + dx, cy + dy, chunkZ);

                if (world.Comp.ChunkMap.ContainsKey(cell) && !world.Comp.LoadedChunks.Contains(cell))
                    LoadChunk(world, cell);
            }
        }
    }

    /// <summary>
    /// Resolves the world, chunk layer and depth offset for a z-level map that belongs to a world.
    /// </summary>
    private bool TryResolveWorldLevel(EntityUid mapUid, out Entity<CEWorldComponent> world, out int chunkZ, out int level)
    {
        world = default;
        chunkZ = 0;
        level = 0;

        if (!_zMapQuery.TryGetComponent(mapUid, out var zMap) ||
            !_worldQuery.TryGetComponent(zMap.NetworkUid, out var comp))
        {
            return false;
        }

        world = (zMap.NetworkUid, comp);
        chunkZ = FloorDiv(zMap.Depth, ChunkHeight);
        level = zMap.Depth - chunkZ * ChunkHeight;
        return true;
    }

    private static Dictionary<int, HashSet<Vector2i>> GetOrCreateModified(CEWorldComponent comp, Vector3i cell)
    {
        if (!comp.ModifiedTiles.TryGetValue(cell, out var byLevel))
        {
            byLevel = new Dictionary<int, HashSet<Vector2i>>();
            comp.ModifiedTiles[cell] = byLevel;
        }

        return byLevel;
    }
}
