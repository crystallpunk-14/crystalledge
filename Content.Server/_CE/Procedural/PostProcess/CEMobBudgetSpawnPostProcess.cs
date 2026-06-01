using System.Threading.Tasks;
using Content.Server._CE.GOAP;
using Content.Shared._CE.Soul.Components;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Procedural;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer dedicated to spawning hostile mobs. Inline entry list (no
/// shared <c>dungeonSpawnTable</c> prototype), always wakes spawned mobs, and
/// distributes a configurable pool of souls across the spawned mobs proportional
/// to each mob's <see cref="BudgetSpawnEntry.Cost"/> (a mob with double the cost
/// drops roughly double the souls). Souls are attached via
/// <see cref="CESoulDropOnDeathComponent"/> so they drop on death.
/// </summary>
public sealed partial class CEMobBudgetSpawnPostProcess : CEDungeonPostProcessLayer
{
    /// <summary>
    /// Total cost-budget available for spawning mobs (analogous to
    /// <see cref="CEBudgetSpawnPostProcess.Budget"/>).
    /// </summary>
    [DataField(required: true)]
    public int Budget;

    /// <summary>
    /// Total pool of souls that will be split between the actually-spawned mobs,
    /// weighted by each mob's <see cref="BudgetSpawnEntry.Cost"/>. With a budget
    /// of 20 and a soul budget of 50, four cost-5 mobs each drop ~12 souls.
    /// </summary>
    [DataField]
    public int SoulBudget = 30;

    /// <summary>
    /// Inline list of mob prototypes that may be picked. No external spawn-table
    /// prototype is referenced.
    /// </summary>
    [DataField(required: true)]
    public List<BudgetSpawnEntry> Entries = new();

    [DataField]
    public List<ProtoId<ContentTileDefinition>>? TileWhitelist;

    [DataField]
    public EntityWhitelist? AnchoredWhitelist;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, int mainZLevel, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var turf = entMan.System<TurfSystem>();
        var whitelistSys = entMan.System<EntityWhitelistSystem>();
        var sleepingSystem = entMan.System<CEGOAPSleepingSystem>();
        var lookup = entMan.System<EntityLookupSystem>();
        var random = new Random();

        var maps = postProcess.GetAllMaps(mapUid);

        var totalWeight = 0f;
        foreach (var entry in Entries)
        {
            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0 || Entries.Count == 0)
            return;

        var candidates = await CollectCandidates(entMan, map, turf, whitelistSys, mapUid, maps, suspend);
        if (candidates.Count == 0)
            return;

        // Shuffle candidates for randomised placement.
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // Spawn pass: track each spawned mob with the cost it was charged at, so we
        // can later allocate souls proportionally to that cost.
        var spawned = new List<(EntityUid Mob, int Cost)>();
        var remaining = Budget;
        var candidateIdx = 0;
        var counter = 0;

        while (remaining > 0 && candidateIdx < candidates.Count)
        {
            if (++counter % 100 == 0)
                await suspend();

            var entry = PickWeightedEntry(random, totalWeight);
            if (entry is null || entry.Cost > remaining)
            {
                entry = FindAffordableEntry(remaining, random);
                if (entry is null)
                    break;
            }

            var (_, _, coords) = candidates[candidateIdx];
            candidateIdx++;

            var mob = entMan.SpawnEntity(entry.Proto, coords);

            // Wake any sleeping mobs at this position immediately — mob layers
            // don't expose a wakeOnSpawn toggle; mobs are always pre-woken.
            WakeAt(sleepingSystem, lookup, coords);

            spawned.Add((mob, entry.Cost));
            remaining -= entry.Cost;
        }

        // Soul allocation: distribute SoulBudget across spawned mobs proportional
        // to each mob's cost. Floor each share and pour the remainder into the most
        // expensive mob so totals match exactly. Skips entirely when SoulBudget <= 0.
        if (SoulBudget > 0 && spawned.Count > 0)
            AllocateSouls(entMan, spawned);
    }

    private void AllocateSouls(IEntityManager entMan, List<(EntityUid Mob, int Cost)> spawned)
    {
        var totalCost = 0;
        foreach (var s in spawned)
        {
            totalCost += s.Cost;
        }

        if (totalCost <= 0)
            return;

        var assigned = 0;
        var maxIdx = 0;
        var maxShare = 0;

        for (var i = 0; i < spawned.Count; i++)
        {
            var share = SoulBudget * spawned[i].Cost / totalCost;
            assigned += share;

            if (share <= 0)
                continue;

            var soul = entMan.EnsureComponent<CESoulDropOnDeathComponent>(spawned[i].Mob);
            soul.Souls = share;

            if (share > maxShare)
            {
                maxShare = share;
                maxIdx = i;
            }
        }

        // Pour the integer-division remainder into the most expensive mob.
        var leftover = SoulBudget - assigned;
        if (leftover > 0 && maxShare > 0)
        {
            var soul = entMan.EnsureComponent<CESoulDropOnDeathComponent>(spawned[maxIdx].Mob);
            soul.Souls += leftover;
        }
    }

    private static void WakeAt(CEGOAPSleepingSystem sleepingSystem, EntityLookupSystem lookup, EntityCoordinates coords)
    {
        var nearby = new HashSet<Entity<CEGOAPSleepingComponent>>();
        lookup.GetEntitiesInRange(coords, 0.5f, nearby);

        foreach (var ent in nearby)
        {
            sleepingSystem.WakeMob(ent);
        }
    }

    private async Task<List<(EntityUid MapUid, Vector2i GridIndices, EntityCoordinates Coords)>> CollectCandidates(
        IEntityManager entMan,
        SharedMapSystem map,
        TurfSystem turf,
        EntityWhitelistSystem whitelistSys,
        EntityUid mapUid,
        List<EntityUid> maps,
        Func<ValueTask> suspend)
    {
        var candidates = new List<(EntityUid MapUid, Vector2i GridIndices, EntityCoordinates Coords)>();
        var counter = 0;
        var excludedZones = BuildExcludedZones(entMan, mapUid);

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 100 == 0)
                    await suspend();

                if (TileWhitelist is { Count: > 0 })
                {
                    var tileDef = turf.GetContentTileDefinition(tileRef);
                    var matched = false;
                    foreach (var allowed in TileWhitelist)
                    {
                        if (tileDef.ID == allowed.Id)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                        continue;
                }

                if (IsInExcludedZone(tileRef.GridIndices, excludedZones))
                    continue;

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
                else if (map.AnchoredEntityCount(uid, grid, tileRef.GridIndices) > 0)
                {
                    continue;
                }

                candidates.Add((uid, tileRef.GridIndices, map.GridTileToLocal(uid, grid, tileRef.GridIndices)));
            }
        }

        return candidates;
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
            if (room.RoomType != null && proto.Resolve(room.RoomType.Value, out var roomTypeProto) && !roomTypeProto.AllowMobsSpawn)
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
