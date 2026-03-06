using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.GOAP.TargetProviders;

/// <summary>
/// Finds the nearest hostile entity across multiple Z-levels.
/// Searches the NPC's own map first, then adjacent levels up/down.
/// <para>
/// Looking DOWN: the NPC can see a target below if LOS on the NPC's own map
/// to the target's same XY is clear (i.e. the floor at that position is transparent/absent).
/// </para>
/// <para>
/// Looking UP: the NPC can see a target above if the tile above the NPC is transparent
/// AND LOS on the upper map from the NPC's XY to the target is clear.
/// </para>
/// </summary>
public sealed partial class CEGOAPCrossZLevelTargetProvider
    : CEGOAPTargetProviderBase<CEGOAPCrossZLevelTargetProvider>
{
    /// <summary>
    /// Detection range in tiles (horizontal only; same for all Z-levels).
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// How many Z-levels up the NPC can detect targets. 0 = same level only.
    /// </summary>
    [DataField]
    public int ZLevelsUp = 1;

    /// <summary>
    /// How many Z-levels down the NPC can detect targets. 0 = same level only.
    /// </summary>
    [DataField]
    public int ZLevelsDown = 1;
}

public sealed partial class CEGOAPCrossZLevelTargetProviderSystem
    : CEGOAPTargetProviderSystem<CEGOAPCrossZLevelTargetProvider>
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<NpcFactionMemberComponent> _factionQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _factionQuery = GetEntityQuery<NpcFactionMemberComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
    }

    protected override void OnResolve(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPResolveTargetEvent<CEGOAPCrossZLevelTargetProvider> args)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var npcWorldPos = _transform.GetWorldPosition(xform);
        var npcMapUid = xform.MapUid;
        if (npcMapUid == null)
            return;

        EntityUid? bestTarget = null;
        var bestDist = float.MaxValue;

        // Search same map first (standard hostile search with LOS)
        SearchMap(ent, npcWorldPos, npcMapUid.Value, 0, args.Provider.VisionRadius, ref bestTarget, ref bestDist);

        // Only do cross-Z search if the map is part of a Z-network
        if (_zMapQuery.HasComponent(npcMapUid.Value))
        {
            // Search maps above
            var currentMap = npcMapUid.Value;
            for (var i = 0; i < args.Provider.ZLevelsUp; i++)
            {
                if (!_zLevels.TryMapUp((currentMap, null), out var aboveMap))
                    break;

                // To look UP: the tile above the NPC at its position must be transparent
                if (!IsTileTransparentAt(aboveMap.Value, npcWorldPos))
                    break; // Can't see further up through opaque tile

                SearchMap(
                    ent, npcWorldPos, aboveMap.Value, 1,
                    args.Provider.VisionRadius, ref bestTarget, ref bestDist);

                currentMap = aboveMap.Value;
            }

            // Search maps below
            currentMap = npcMapUid.Value;
            for (var i = 0; i < args.Provider.ZLevelsDown; i++)
            {
                if (!_zLevels.TryMapDown((currentMap, null), out var belowMap))
                    break;

                SearchMap(
                    ent, npcWorldPos, belowMap.Value, -1,
                    args.Provider.VisionRadius, ref bestTarget, ref bestDist);

                currentMap = belowMap.Value;
            }
        }

        if (bestTarget != null)
        {
            args.TargetEntity = bestTarget;
            if (_xformQuery.TryGetComponent(bestTarget.Value, out var targetXform))
                args.TargetCoordinates = targetXform.Coordinates;
        }
    }

    /// <summary>
    /// Searches a single map for hostile entities within range.
    /// </summary>
    /// <param name="ent">The NPC entity.</param>
    /// <param name="npcWorldPos">NPC's world position (XY shared across Z-levels).</param>
    /// <param name="searchMapUid">The map to search on.</param>
    /// <param name="zDirection">0 = same map, 1 = above, -1 = below.</param>
    /// <param name="range">Vision radius.</param>
    /// <param name="bestTarget">Tracking best target found so far.</param>
    /// <param name="bestDist">Tracking best distance found so far.</param>
    private void SearchMap(
        Entity<CEGOAPComponent> ent,
        Vector2 npcWorldPos,
        EntityUid searchMapUid,
        int zDirection,
        float range,
        ref EntityUid? bestTarget,
        ref float bestDist)
    {
        if (!TryComp<MapComponent>(searchMapUid, out var mapComp))
            return;

        var searchMapId = mapComp.MapId;
        var probeCoords = new MapCoordinates(npcWorldPos, searchMapId);

        // Find faction members on this map within range
        foreach (var found in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(probeCoords, range))
        {
            if (found.Owner == ent.Owner)
                continue;

            // Check hostility
            if (!IsHostile(ent, found))
                continue;

            if (!_xformQuery.TryGetComponent(found, out var targetXform))
                continue;

            var targetWorldPos = _transform.GetWorldPosition(targetXform);
            var dist = Vector2.Distance(npcWorldPos, targetWorldPos);

            if (dist >= bestDist)
                continue;

            // Perform the appropriate LOS check based on Z-direction
            if (!CheckLos(ent, npcWorldPos, found, targetWorldPos, searchMapUid, zDirection, range))
                continue;

            bestDist = dist;
            bestTarget = found;
        }
    }

    /// <summary>
    /// Performs a line-of-sight check appropriate for the Z-direction.
    /// </summary>
    private bool CheckLos(
        Entity<CEGOAPComponent> ent,
        Vector2 npcWorldPos,
        EntityUid target,
        Vector2 targetWorldPos,
        EntityUid searchMapUid,
        int zDirection,
        float range)
    {
        if (zDirection == 0)
        {
            // Same map: standard LOS check between entities
            return _examine.InRangeUnOccluded(ent.Owner, target, range + 0.5f);
        }

        if (!TryComp<MapComponent>(searchMapUid, out var searchMapComp))
            return false;

        if (zDirection > 0)
        {
            // Looking UP: LOS check on the upper map (from NPC's XY to target position)
            var npcProbe = new MapCoordinates(npcWorldPos, searchMapComp.MapId);
            var targetCoords = new MapCoordinates(targetWorldPos, searchMapComp.MapId);
            return _examine.InRangeUnOccluded(npcProbe, targetCoords, range + 0.5f, null);
        }

        // Looking DOWN: LOS check on the NPC's own map (from NPC position to target's XY)
        // If the NPC can see the floor at the target's XY on its own level, it can see down.
        if (!_xformQuery.TryGetComponent(ent, out var npcXform) || npcXform.MapUid == null)
            return false;

        if (!TryComp<MapComponent>(npcXform.MapUid.Value, out var npcMapComp))
            return false;

        var npcMapCoords = new MapCoordinates(npcWorldPos, npcMapComp.MapId);
        var probeOnNpcMap = new MapCoordinates(targetWorldPos, npcMapComp.MapId);
        return _examine.InRangeUnOccluded(npcMapCoords, probeOnNpcMap, range + 0.5f, null);
    }

    /// <summary>
    /// Checks if the tile at the given world position on the specified map is transparent.
    /// </summary>
    private bool IsTileTransparentAt(EntityUid mapUid, Vector2 worldPos)
    {
        if (!_gridQuery.TryGetComponent(mapUid, out var grid))
            return true; // No grid = open air = transparent

        if (!_mapSystem.TryGetTileRef(mapUid, grid, worldPos, out var tileRef))
            return true; // No tile = transparent

        if (tileRef.Tile.IsEmpty)
            return true;

        var tileDef = (ContentTileDefinition) _tileDef[tileRef.Tile.TypeId];
        return tileDef.Transparent;
    }

    /// <summary>
    /// Checks if a found entity is hostile to the NPC using the faction system's public API.
    /// Iterates the NPC's factions (read-only access) and checks each via IsFactionHostile.
    /// </summary>
    private bool IsHostile(Entity<CEGOAPComponent> npc, Entity<NpcFactionMemberComponent> target)
    {
        if (!_factionQuery.TryGetComponent(npc, out var npcFaction))
            return false;

        // Check if any of the NPC's factions consider the target hostile
        foreach (var factionId in npcFaction.Factions)
        {
            if (_faction.IsFactionHostile(factionId, (target.Owner, target.Comp)))
                return true;
        }

        return false;
    }
}
