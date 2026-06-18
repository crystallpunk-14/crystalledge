using System.Numerics;
using Content.Shared._CE.Maths;
using Content.Shared._CE.WorldGen.PostProcess;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.WorldGen.Generators;

/// <summary>
/// Data-only base class for chunk generators.
/// Logic is handled by systems subscribing to <see cref="CEChunkGenEvent{T}"/>,
/// mirroring the CE entity-effect pattern (data + handling system in one file).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEChunkGenerator
{
    /// <summary>
    /// Dispatches this generator by raising a typed broadcast event.
    /// </summary>
    public abstract void Generate(CEChunkGenArgs args);
}

/// <summary>
/// Generic base that provides automatic event dispatch for concrete generator types.
/// Concrete generators inherit from this instead of <see cref="CEChunkGenerator"/> directly.
/// </summary>
public abstract partial class CEChunkGeneratorBase<T> : CEChunkGenerator where T : CEChunkGeneratorBase<T>
{
    public override void Generate(CEChunkGenArgs args)
    {
        if (this is not T typed)
            return;

        var ev = new CEChunkGenEvent<T>(typed, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

/// <summary>
/// What a generator wants placed on a single tile: at most one tile, one entity, one decal.
/// A null <see cref="Tile"/> means the cell stays void — entity and decal are then ignored.
/// </summary>
public struct CETileContent
{
    /// <summary>
    /// Tile to place (null == leave the cell void; entity and decal are ignored).
    /// </summary>
    public ProtoId<ContentTileDefinition>? Tile;

    /// <summary>
    /// Entity to spawn on this tile.
    /// </summary>
    public CEEntitySpec? Entity;

    /// <summary>
    /// Decal to place on this tile.
    /// </summary>
    public CEDecalSpec? Decal;

    /// <summary>
    /// Resets all fields to empty for reuse across tiles.
    /// </summary>
    public void Clear()
    {
        Tile = null;
        Entity = null;
        Decal = null;
    }
}

/// <summary>
/// One entity to spawn on a tile, carrying its rotation and whether it should be anchored.
/// </summary>
public readonly record struct CEEntitySpec(EntProtoId Proto, Angle Rotation, bool Anchored = true);

/// <summary>
/// One decal to place on a tile. <paramref name="Offset"/> is the sub-tile position within the
/// tile (grid units, already including any rotation nudge), <paramref name="Rotation"/> the final
/// decal angle.
/// </summary>
public readonly record struct CEDecalSpec(
    ProtoId<DecalPrototype> Id,
    Vector2 Offset,
    Angle Rotation,
    Color? Color = null,
    bool Cleanable = false);

/// <summary>
/// Per-tile input handed to a generator. Deliberately carries NO write access to the grid — a
/// generator can only describe content for the asked tile, never write outside its chunk. The base
/// system owns the loop, bounds, batching and load/unload bookkeeping. The <see cref="Grid"/> field
/// is provided for read-only per-tile queries (e.g. by post-process biome layers) but must not be
/// used to write tiles.
/// </summary>
public readonly struct CETileGenContext(
    IEntityManager entityManager,
    Vector3i chunkCoord,
    Vector2i localTile,
    int level,
    Vector2i worldTile,
    int depth,
    int seed,
    Entity<MapGridComponent> grid)
{
    /// <summary>
    /// Entity manager for read-only lookups.
    /// </summary>
    public readonly IEntityManager EntityManager = entityManager;

    /// <summary>
    /// Chunk this tile belongs to (x, y, chunk-z).
    /// </summary>
    public readonly Vector3i ChunkCoord = chunkCoord;

    /// <summary>
    /// Tile position inside the chunk: 0..<c>CEWorldGenConstants.ChunkSize</c>-1.
    /// </summary>
    public readonly Vector2i LocalTile = localTile;

    /// <summary>
    /// Z-level offset within the chunk: 0..<c>CEWorldGenConstants.ChunkHeight</c>-1.
    /// </summary>
    public readonly int Level = level;

    /// <summary>
    /// Absolute tile index on the level grid.
    /// </summary>
    public readonly Vector2i WorldTile = worldTile;

    /// <summary>
    /// Absolute world depth of this level.
    /// </summary>
    public readonly int Depth = depth;

    /// <summary>
    /// World seed.
    /// </summary>
    public readonly int Seed = seed;

    /// <summary>
    /// The level grid this tile lives on. Passed to post-process layers (e.g. biome system) for
    /// read-only per-tile queries; generators themselves must not write to the grid directly.
    /// </summary>
    public readonly Entity<MapGridComponent> Grid = grid;
}

/// <summary>
/// Everything a chunk generator needs to fill a whole chunk — all of its z-levels.
/// A chunk spans <see cref="LevelGrids"/>.Count z-levels; the generator is responsible
/// for filling every level. The reference-typed output collections are populated by the
/// base system and read back by the world-gen system for load/unload bookkeeping.
/// </summary>
public sealed class CEChunkGenArgs(
    IEntityManager entityManager,
    IReadOnlyList<Entity<MapGridComponent>> levelGrids,
    Vector3i chunkCoord,
    Vector2i chunkOriginTile,
    int seed,
    IReadOnlyList<HashSet<Vector2i>> modifiedTilesPerLevel,
    List<(int Level, Vector2i Tile, Tile Value)> generatedTiles,
    List<(int Level, EntityUid Ent, Vector2i Tile)> spawnedEntities,
    IReadOnlyList<CEWorldPostProcessLayer> postProcess)
{
    /// <summary>
    /// Entity manager for the generator dispatch.
    /// </summary>
    public readonly IEntityManager EntityManager = entityManager;

    /// <summary>
    /// One grid per z-level of the chunk; index == depth offset (0 == bottom).
    /// </summary>
    public readonly IReadOnlyList<Entity<MapGridComponent>> LevelGrids = levelGrids;

    /// <summary>
    /// Chunk coordinate (x, y, chunk-z).
    /// </summary>
    public readonly Vector3i ChunkCoord = chunkCoord;

    /// <summary>
    /// Bottom-left tile of the chunk on the level grid.
    /// </summary>
    public readonly Vector2i ChunkOriginTile = chunkOriginTile;

    /// <summary>
    /// World seed.
    /// </summary>
    public readonly int Seed = seed;

    /// <summary>
    /// Tiles already edited by players, per level — never overwrite these.
    /// </summary>
    public readonly IReadOnlyList<HashSet<Vector2i>> ModifiedTilesPerLevel = modifiedTilesPerLevel;

    /// <summary>
    /// Output: every tile the generator placed, tagged with its level, for unload diffing.
    /// </summary>
    public readonly List<(int Level, Vector2i Tile, Tile Value)> GeneratedTiles = generatedTiles;

    /// <summary>
    /// Output: every entity the generator spawned, tagged with its level, for unload cleanup.
    /// </summary>
    public readonly List<(int Level, EntityUid Ent, Vector2i Tile)> SpawnedEntities = spawnedEntities;

    /// <summary>
    /// Ordered post-process layers applied per-tile after <see cref="CEChunkGeneratorSystem{T}.GenerateTile"/>.
    /// </summary>
    public readonly IReadOnlyList<CEWorldPostProcessLayer> PostProcess = postProcess;
}

/// <summary>
/// Broadcast event raised when a chunk generator is dispatched.
/// </summary>
[ByRefEvent]
public record struct CEChunkGenEvent<T>(T Generator, CEChunkGenArgs Args) where T : CEChunkGeneratorBase<T>;
