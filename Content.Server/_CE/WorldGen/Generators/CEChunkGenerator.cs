using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.WorldGen.Generators;

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
/// Everything a chunk generator needs to fill a whole chunk — all of its z-levels.
/// A chunk spans <see cref="LevelGrids"/>.Count z-levels; the generator is responsible
/// for filling every level. The reference-typed output collections are populated by the
/// generator and read back by the world-gen system for load/unload bookkeeping.
/// </summary>
public sealed class CEChunkGenArgs(
    IEntityManager entityManager,
    IReadOnlyList<Entity<MapGridComponent>> levelGrids,
    Vector2i chunkOriginTile,
    int seed,
    IReadOnlyList<HashSet<Vector2i>> modifiedTilesPerLevel,
    List<(int Level, Vector2i Tile, Tile Value)> generatedTiles,
    List<(int Level, EntityUid Ent, Vector2i Tile)> spawnedEntities)
{
    public readonly IEntityManager EntityManager = entityManager;

    /// <summary>
    /// One grid per z-level of the chunk; index == depth offset (0 == bottom).
    /// </summary>
    public readonly IReadOnlyList<Entity<MapGridComponent>> LevelGrids = levelGrids;

    public readonly Vector2i ChunkOriginTile = chunkOriginTile;
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
}

/// <summary>
/// Broadcast event raised when a chunk generator is dispatched.
/// </summary>
[ByRefEvent]
public record struct CEChunkGenEvent<T>(T Generator, CEChunkGenArgs Args) where T : CEChunkGeneratorBase<T>;

/// <summary>
/// Abstract base system for handling chunk generators.
/// Concrete systems inherit this and implement <see cref="Generate"/>.
/// </summary>
public abstract partial class CEChunkGeneratorSystem<T> : EntitySystem where T : CEChunkGeneratorBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEChunkGenEvent<T>>(OnGenerate);
    }

    private void OnGenerate(ref CEChunkGenEvent<T> args)
    {
        Generate(ref args);
    }

    protected abstract void Generate(ref CEChunkGenEvent<T> args);
}
