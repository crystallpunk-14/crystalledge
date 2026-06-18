using System.Threading.Tasks;
using Content.Shared._CE.Procedural;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: spends a budget to spawn entries resolved via <see cref="EntityTableSelector"/>
/// across dungeon tiles. Each entry rolls its table to produce one or more entity prototypes.
/// Supports optional filtering by tile type and anchored entity whitelist (e.g., tables).
/// </summary>
public sealed partial class CEEntityTableBudgetSpawnPostProcess : CEDungeonPostProcessLayer
{
    /// <summary>
    /// Total budget available for this layer.
    /// </summary>
    [DataField(required: true)]
    public int Budget;

    /// <summary>
    /// Weighted list of entries that can be spawned.
    /// </summary>
    [DataField(required: true)]
    public List<EntityTableBudgetEntry> Entries = new();

    /// <summary>
    /// If set, only spawn on tiles whose prototype ID is in this list.
    /// </summary>
    [DataField]
    public List<ProtoId<ContentTileDefinition>>? TileWhitelist;

    /// <summary>
    /// If set, only spawn on tiles that have at least one anchored entity matching this whitelist.
    /// Useful for spawning loot on tables, shelves, etc.
    /// </summary>
    [DataField]
    public EntityWhitelist? AnchoredWhitelist;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, int mainZLevel, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var turf = entMan.System<TurfSystem>();
        var whitelistSys = entMan.System<EntityWhitelistSystem>();
        var entityTable = entMan.System<EntityTableSystem>();
        var random = new Random();

        var maps = postProcess.GetAllMaps(mapUid);

        var totalWeight = 0f;
        foreach (var entry in Entries)
        {
            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0 || Entries.Count == 0)
            return;

        // Collect all valid spawn positions across all z-levels.
        var candidates = new List<(EntityUid MapUid, Vector2i GridIndices, EntityCoordinates Coords)>();
        var counter = 0;

        // Build excluded zones from room data on the map.
        var excludedZones = BuildExcludedZones(entMan, mapUid);

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 100 == 0)
                    await suspend();

                // Tile whitelist filter.
                if (TileWhitelist is { Count: > 0 })
                {
                    var tileDef = turf.GetContentTileDefinition(tileRef);
                    var match = false;
                    foreach (var allowed in TileWhitelist)
                    {
                        if (tileDef.ID == allowed.Id)
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match)
                        continue;
                }

                // Room type exclusion filter.
                if (IsInExcludedZone(tileRef.GridIndices, excludedZones))
                    continue;

                // Anchored entity whitelist filter.
                if (AnchoredWhitelist is not null)
                {
                    var anchored = map.GetAnchoredEntitiesEnumerator(uid, grid, tileRef.GridIndices);
                    var hasMatch = false;
                    while (anchored.MoveNext(out var anchoredUid))
                    {
                        if (whitelistSys.IsValid(AnchoredWhitelist, anchoredUid.Value))
                        {
                            hasMatch = true;
                            break;
                        }
                    }
                    if (!hasMatch)
                        continue;
                }
                else
                {
                    // When no anchored whitelist, skip tiles that already have anchored entities
                    // (walls, furniture, etc.) to avoid stacking.
                    if (map.AnchoredEntityCount(uid, grid, tileRef.GridIndices) > 0)
                        continue;
                }

                candidates.Add((uid, tileRef.GridIndices, map.GridTileToLocal(uid, grid, tileRef.GridIndices)));
            }
        }

        if (candidates.Count == 0)
            return;

        // Shuffle candidates for randomized placement.
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // Spend the budget.
        var remaining = Budget;
        var candidateIdx = 0;

        while (remaining > 0 && candidateIdx < candidates.Count)
        {
            if (++counter % 100 == 0)
                await suspend();

            // Pick a weighted random entry.
            var entry = PickWeightedEntry(random, totalWeight);
            if (entry is null || entry.Cost > remaining)
            {
                // Try to find any affordable entry.
                entry = FindAffordableEntry(remaining, random);
                if (entry is null)
                    break;
            }

            // Each proto from the table gets its own candidate tile.
            foreach (var proto in entityTable.GetSpawns(entry.Table, random))
            {
                if (candidateIdx >= candidates.Count)
                    break;
                var (_, _, coords) = candidates[candidateIdx++];
                entMan.SpawnEntity(proto, coords);
            }

            remaining -= entry.Cost;
        }
    }

    private EntityTableBudgetEntry? PickWeightedEntry(Random random, float totalWeight)
    {
        var roll = random.NextSingle() * totalWeight;
        var cumulative = 0f;
        foreach (var entry in Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
                return entry;
        }
        return Entries.Count > 0 ? Entries[^1] : null;
    }

    private EntityTableBudgetEntry? FindAffordableEntry(int remaining, Random random)
    {
        var affordableWeight = 0f;
        foreach (var entry in Entries)
        {
            if (entry.Cost <= remaining)
                affordableWeight += entry.Weight;
        }

        if (affordableWeight <= 0)
            return null;

        var roll = random.NextSingle() * affordableWeight;
        var cumulative = 0f;
        foreach (var entry in Entries)
        {
            if (entry.Cost > remaining)
                continue;
            cumulative += entry.Weight;
            if (roll <= cumulative)
                return entry;
        }
        return null;
    }

    private List<(Vector2i Pos, Vector2i Size)> BuildExcludedZones(IEntityManager entMan, EntityUid mapUid)
    {
        var zones = new List<(Vector2i Pos, Vector2i Size)>();

        if (!entMan.TryGetComponent<CEGeneratingProceduralDungeonComponent>(mapUid, out var dungeon))
            return zones;

        var proto = IoCManager.Resolve<IPrototypeManager>();
        foreach (var room in dungeon.Rooms)
        {
            if (room.RoomType != null && proto.Resolve(room.RoomType.Value, out var roomTypeProto) && !roomTypeProto.AllowLootSpawn)
                zones.Add((room.Position, room.Size));
        }

        return zones;
    }

    private static bool IsInExcludedZone(Vector2i tileIndices, List<(Vector2i Pos, Vector2i Size)> zones)
    {
        foreach (var (pos, size) in zones)
        {
            if (tileIndices.X >= pos.X && tileIndices.X < pos.X + size.X &&
                tileIndices.Y >= pos.Y && tileIndices.Y < pos.Y + size.Y)
                return true;
        }
        return false;
    }
}

/// <summary>
/// A single entry in the entity-table budget spawn list.
/// </summary>
[DataDefinition]
public sealed partial class EntityTableBudgetEntry
{
    /// <summary>
    /// Entity table selector to roll when this entry is picked.
    /// Can produce one or more entity prototypes.
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector Table = default!;

    /// <summary>
    /// Budget cost for picking this entry once.
    /// </summary>
    [DataField]
    public int Cost = 1;

    /// <summary>
    /// Relative weight for random selection. Higher = more likely.
    /// </summary>
    [DataField]
    public float Weight = 1f;
}
