using System.Threading.Tasks;
using Content.Shared._CE.GOAP;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: spends a budget to spawn weighted entries across dungeon tiles.
/// Supports optional filtering by tile type and anchored entity whitelist (e.g., tables).
/// Each entry has a cost; the layer keeps spawning until the budget runs out.
/// </summary>
public sealed partial class CEBudgetSpawnPostProcess : CEDungeonPostProcessLayer
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
    public List<BudgetSpawnEntry> Entries = new();

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

    /// <summary>
    /// If true, spawned entities with <see cref="CEGOAPComponent"/> will have their
    /// <see cref="CEGOAPSleepingComponent"/> removed so they are immediately active.
    /// </summary>
    [DataField]
    public bool WakeOnSpawn;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var turf = entMan.System<TurfSystem>();
        var whitelistSys = entMan.System<EntityWhitelistSystem>();
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

            // Find the next valid candidate position.
            if (candidateIdx >= candidates.Count)
                break;

            var (_, _, coords) = candidates[candidateIdx];
            candidateIdx++;

            entMan.SpawnEntity(entry.Proto, coords);

            // Wake mob if requested.
            if (WakeOnSpawn)
                WakeSpawnedEntity(entMan, coords);

            remaining -= entry.Cost;
        }
    }

    private BudgetSpawnEntry? PickWeightedEntry(Random random, float totalWeight)
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

    private BudgetSpawnEntry? FindAffordableEntry(int remaining, Random random)
    {
        // Build a sub-list of affordable entries and pick one.
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

    private static void WakeSpawnedEntity(IEntityManager entMan, EntityCoordinates coords)
    {
        var sleepingSystem = entMan.System<GOAP.CEGOAPSleepingSystem>();
        var lookup = entMan.System<EntityLookupSystem>();

        var nearby = new HashSet<Entity<CEGOAPSleepingComponent>>();
        lookup.GetEntitiesInRange(coords, 0.5f, nearby);

        foreach (var ent in nearby)
        {
            sleepingSystem.WakeMob(ent);
        }
    }
}

/// <summary>
/// A single entry in the budget spawn table.
/// </summary>
[DataDefinition]
public sealed partial class BudgetSpawnEntry
{
    /// <summary>
    /// Entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// Budget cost for spawning one of this entity.
    /// </summary>
    [DataField(required: true)]
    public int Cost = 1;

    /// <summary>
    /// Relative weight for random selection. Higher = more likely.
    /// </summary>
    [DataField]
    public float Weight = 1f;
}
