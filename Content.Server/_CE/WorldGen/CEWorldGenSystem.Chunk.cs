using Content.Server._CE.WorldGen.Generators;
using Content.Shared._CE.Maths;
using Content.Shared._CE.WorldGen.Components;
using Content.Shared._CE.WorldGen.Generators;
using Content.Shared._CE.WorldGen.Prototypes;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen;

/// <summary>
/// Periodic chunk loading/unloading around players, plus the load/unload internals.
/// Mirrors the upstream biome load loop (pooled scratch sets, cached entity queries),
/// adapted to 3D chunks across a z-network, and preserves player edits via tile diffing
/// + <see cref="IEntityManager.IsDefault"/>.
///
/// Loading stays synchronous: tile-setting and entity spawning must run on the main thread, so
/// there is nothing to gain from an async job (unlike biome marker math). Instead the active-chunk
/// scan is throttled to <see cref="UpdateInterval"/> — players cross 8-tile chunk boundaries rarely,
/// and the load radius covers any movement within one interval.
/// </summary>
public sealed partial class CEWorldGenSystem
{
    /// <summary>
    /// Seconds between active-chunk scans.
    /// </summary>
    private const float UpdateInterval = 0.5f;

    private float _accumulator;

    private readonly List<Vector3i> _toUnload = new();

    // Reused transient scratch for a single LoadChunk call (consumed synchronously by the generator).
    private readonly List<Entity<MapGridComponent>> _levelGrids = new();
    private readonly List<HashSet<Vector2i>> _modifiedPerLevel = new();

    // Shared immutable "no edits" set — generators only read it, so all unmodified levels can share one.
    private static readonly HashSet<Vector2i> EmptyTiles = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        // One pooled active-set per world.
        var worldQuery = AllEntityQuery<CEWorldComponent>();
        while (worldQuery.MoveNext(out var uid, out _))
        {
            _activeChunks[uid] = _chunkSetPool.Get();
        }

        if (_activeChunks.Count == 0)
            return;

        // Gather the chunks in range of every player.
        foreach (var session in Filter.GetAllPlayers(_playerManager))
        {
            if (session.AttachedEntity is not { } player)
                continue;

            if (!_xformQuery.TryGetComponent(player, out var xform) || xform.MapUid is not { } mapUid)
                continue;

            if (!_zMapQuery.TryGetComponent(mapUid, out var zMap))
                continue;

            if (!_activeChunks.TryGetValue(zMap.NetworkUid, out var set) ||
                !_worldQuery.TryGetComponent(zMap.NetworkUid, out var world))
            {
                continue;
            }

            var worldPos = _transform.GetWorldPosition(xform);
            var cx = (int)MathF.Floor(worldPos.X / ChunkSize);
            var cy = (int)MathF.Floor(worldPos.Y / ChunkSize);
            var radius = (int)MathF.Ceiling(LoadRadiusChunks);

            // Fire a location-change event for the chunk the player actually stands in. The chunk type
            // only exists where the plan painted one; void leaves the last known location untouched.
            // This loop only detects the transition — resolving any title is the announce system's job.
            var playerChunk = new Vector3i(cx, cy, FloorDiv(zMap.Depth, ChunkHeight));
            if (world.ChunkMap.TryGetValue(playerChunk, out var standingType))
            {
                var visitor = EnsureComp<CELocationVisitorComponent>(player);
                if (visitor.CurrentLocation != standingType)
                {
                    // Broadcast so the component-filtered announce handler and any future global listeners fire.
                    var moveEv = new CEMoveToNewLocation(player, visitor.CurrentLocation, standingType);
                    RaiseLocalEvent(player, ref moveEv, broadcast: true);
                    visitor.CurrentLocation = standingType;
                }
            }

            // The player can see down through several z-levels (and one up). Keep every chunk layer in
            // that visible depth range loaded, or the floors below would render as void.
            var minChunkZ = FloorDiv(zMap.Depth - CESharedZLevelsSystem.MaxZLevelsBelowRendering, ChunkHeight);
            var maxChunkZ = FloorDiv(zMap.Depth + 1, ChunkHeight);

            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var cz = minChunkZ; cz <= maxChunkZ; cz++)
                    {
                        var cell = new Vector3i(cx + dx, cy + dy, cz);

                        // Void outside the painted plan.
                        if (world.ChunkMap.ContainsKey(cell))
                            set.Add(cell);
                    }
                }
            }
        }

        // Apply: load newly-active chunks, unload no-longer-active ones.
        var loadQuery = AllEntityQuery<CEWorldComponent>();
        while (loadQuery.MoveNext(out var uid, out var world))
        {
            if (!_activeChunks.TryGetValue(uid, out var set))
                continue;

            // Pinned chunks are always active — inject before the load pass so they're never unloaded.
            foreach (var pinned in world.PinnedChunks)
                set.Add(pinned);

            foreach (var cell in set)
            {
                if (!world.LoadedChunks.Contains(cell))
                    LoadChunk((uid, world), cell);
            }

            _toUnload.Clear();
            foreach (var cell in world.LoadedChunks)
            {
                if (!set.Contains(cell))
                    _toUnload.Add(cell);
            }

            foreach (var cell in _toUnload)
            {
                UnloadChunk((uid, world), cell);
            }
        }

        // Recycle pooled sets.
        foreach (var set in _activeChunks.Values)
        {
            _chunkSetPool.Return(set);
        }

        _activeChunks.Clear();
    }

    private void LoadChunk(Entity<CEWorldComponent> world, Vector3i chunk)
    {
        var comp = world.Comp;

        if (!comp.ChunkMap.TryGetValue(chunk, out var typeId) || !_proto.TryIndex(typeId, out var chunkType))
            return;

        _levelGrids.Clear();
        _modifiedPerLevel.Clear();
        comp.ModifiedTiles.TryGetValue(chunk, out var modifiedByLevel);

        for (var level = 0; level < ChunkHeight; level++)
        {
            if (!TryGetLevelGrid(world.Owner, chunk, level, out var grid))
            {
                Log.Error($"CEWorldGen: missing z-level grid for chunk {chunk} level {level}.");
                return;
            }

            _levelGrids.Add(grid);

            // Common path (no player edits in this chunk): reuse the shared empty set, no allocation.
            var mod = modifiedByLevel != null && modifiedByLevel.TryGetValue(level, out var existing)
                ? existing
                : EmptyTiles;

            _modifiedPerLevel.Add(mod);
        }

        var originTile = new Vector2i(chunk.X * ChunkSize, chunk.Y * ChunkSize);
        var generated = new List<(int Level, Vector2i Tile, Tile Value)>();
        var spawned = new List<(int Level, EntityUid Ent, Vector2i Tile)>();

        var args = new CEChunkGenArgs(
            EntityManager,
            _levelGrids,
            chunk,
            originTile,
            comp.Seed,
            _modifiedPerLevel,
            generated,
            spawned,
            chunkType.PostProcess);

        chunkType.Generator.Generate(args);

        comp.LoadedEntities[chunk] = spawned;
        comp.GeneratedTiles[chunk] = generated;
        comp.LoadedChunks.Add(chunk);
    }

    private void UnloadChunk(Entity<CEWorldComponent> world, Vector3i chunk)
    {
        var comp = world.Comp;
        var modifiedByLevel = comp.ModifiedTiles.TryGetValue(chunk, out var existing)
            ? existing
            : new Dictionary<int, HashSet<Vector2i>>();

        // Entities FIRST: delete pristine ones; keep anything a player touched (moved/unanchored/!IsDefault).
        // Keeping an entity marks its tile modified, which the tile pass below then refuses to clear — so a
        // kept (e.g. damaged) entity never ends up floating over a removed tile.
        if (comp.LoadedEntities.TryGetValue(chunk, out var ents))
        {
            foreach (var (level, ent, tile) in ents)
            {
                if (ShouldKeepEntity(world.Owner, chunk, level, ent, tile))
                    MarkModified(modifiedByLevel, level, tile);
                else
                    Del(ent);
            }

            comp.LoadedEntities.Remove(chunk);
        }

        // Tiles SECOND: clear tiles still matching what we generated; keep (mark modified) any the player
        // changed, and skip tiles already marked modified above (those under kept entities).
        if (comp.GeneratedTiles.TryGetValue(chunk, out var gen))
        {
            var emptyByLevel = new Dictionary<int, List<(Vector2i, Tile)>>();

            foreach (var (level, tile, value) in gen)
            {
                if (modifiedByLevel.TryGetValue(level, out var modSet) && modSet.Contains(tile))
                    continue;

                if (!TryGetLevelGrid(world.Owner, chunk, level, out var grid))
                    continue;

                // Don't mess with anything that's potentially anchored (our kept entity, or one the
                // player built/placed that we don't track) — clearing the tile would unanchor it.
                var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, tile);

                if (anchored.MoveNext(out _))
                {
                    MarkModified(modifiedByLevel, level, tile);
                    continue;
                }

                // If it's still the data we generated, unload the tile; otherwise it's custom — keep it.
                if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) || tileRef.Tile != value)
                {
                    MarkModified(modifiedByLevel, level, tile);
                    continue;
                }

                if (!emptyByLevel.TryGetValue(level, out var list))
                {
                    list = new List<(Vector2i, Tile)>();
                    emptyByLevel[level] = list;
                }

                list.Add((tile, Tile.Empty));
            }

            foreach (var (level, list) in emptyByLevel)
            {
                if (TryGetLevelGrid(world.Owner, chunk, level, out var grid))
                    _map.SetTiles(grid.Owner, grid.Comp, list);
            }

            comp.GeneratedTiles.Remove(chunk);
        }

        comp.LoadedChunks.Remove(chunk);

        if (modifiedByLevel.Count == 0)
            comp.ModifiedTiles.Remove(chunk);
        else
            comp.ModifiedTiles[chunk] = modifiedByLevel;
    }

    /// <summary>
    /// True if a generated entity must be preserved across unload.
    ///
    /// An entity is kept if it has moved away from its spawn tile (player relocated it). Anchored
    /// state is NOT used as the primary signal: worldgen can legitimately spawn unanchored entities
    /// (biome vegetation, ambient spawners) and those must also unload when pristine.
    ///
    /// An entity at its spawn tile is deleted unless <see cref="IEntityManager.IsDefault"/> says its
    /// state diverged from the prototype — which means a player actively modified it (damaged, edited
    /// a data field). Entities that self-mutate on spawn (e.g. light behaviour, physics fixtures) must
    /// declare those fields in their prototype so <see cref="IEntityManager.IsDefault"/> considers them
    /// default; see <c>CEBaseWallmount</c> for the Fixtures pattern.
    /// </summary>
    private bool ShouldKeepEntity(EntityUid worldUid, Vector3i chunk, int level, EntityUid ent, Vector2i tile)
    {
        if (Deleted(ent) || !_xformQuery.TryGetComponent(ent, out var xform))
            return true;

        if (!TryGetLevelGrid(worldUid, chunk, level, out var grid))
            return true;

        // Entity moved away from its spawn tile — a player relocated it; keep it.
        if (_map.LocalToTile(grid.Owner, grid.Comp, xform.Coordinates) != tile)
            return true;

        var nonDefault = !EntityManager.IsDefault(ent);

#if DEBUG || TOOLS
        // Diagnostic: log which components have non-default data so self-mutating entities can be fixed.
        if (nonDefault && MetaData(ent).EntityPrototype is { } diagProto)
        {
            var protoComps = diagProto.Components;
            var nonDefaultComps = new List<string>();
            foreach (var comp in EntityManager.GetComponents(ent))
            {
                var compType = comp.GetType();
                if (compType == typeof(TransformComponent) || compType == typeof(MetaDataComponent))
                    continue;

                var name = EntityManager.ComponentFactory.GetComponentName(compType);
                if (!protoComps.ContainsKey(name))
                    nonDefaultComps.Add($"+{name}(runtime)");
                // Individual field diffs need VV or profiling; can't cheaply detect here without
                // duplicating IEntityManager.IsDefault internals.
            }

            var runtimeStr = nonDefaultComps.Count > 0 ? string.Join(",", nonDefaultComps) : "none";
            Log.Warning($"[CEWG-DIAG] keep '{diagProto.ID}': anchored={xform.Anchored} runtime-added=[{runtimeStr}] — if empty, a component FIELD diverged from prototype (use VV to inspect).");
        }
#endif
        return nonDefault;
    }

    private bool TryGetLevelGrid(EntityUid worldUid, Vector3i chunk, int level, out Entity<MapGridComponent> grid)
    {
        grid = default;
        var depth = chunk.Z * ChunkHeight + level;


        if (!_zLevels.TryGetMapAtDepth(worldUid, depth, out var gridUid) ||
            !_gridQuery.TryGetComponent(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        return true;
    }

    private static void MarkModified(Dictionary<int, HashSet<Vector2i>> modifiedByLevel, int level, Vector2i tile)
    {
        if (!modifiedByLevel.TryGetValue(level, out var set))
        {
            set = new HashSet<Vector2i>();
            modifiedByLevel[level] = set;
        }

        set.Add(tile);
    }
}

/// <summary>
/// Raised on a player entity when it crosses a chunk boundary into a location (chunk type) different
/// from the one it was previously standing in. <see cref="From"/> is null for the very first location
/// a player is resolved into. The worldgen loop only reports the transition; consumers decide what,
/// if anything, to do with it (e.g. the location-title popup).
/// </summary>
[ByRefEvent]
public readonly record struct CEMoveToNewLocation(
    EntityUid Player,
    ProtoId<CEWorldChunkTypePrototype>? From,
    ProtoId<CEWorldChunkTypePrototype> To);
