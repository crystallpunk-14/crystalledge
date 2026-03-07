using System.Numerics;
using Content.Shared._CE.GOAP;
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
/// Searches the NPC's own map first, then 1 adjacent levels up/down.
/// <para>
/// Looking DOWN: Horizontal LOS check first, tile transparency check second
/// <para>
/// ___A_----------->↓
/// </para>
///.........|____________B
/// </para>
/// <para>
/// Looking UP: Tile transparency above entity first, horizontal LOS check second
/// </para>
/// ...↑---------->B____
/// <para>
/// __A____________|
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
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<NpcFactionMemberComponent> _factionQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _factionQuery = GetEntityQuery<NpcFactionMemberComponent>();
    }

    protected override void OnResolve(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPResolveTargetEvent<CEGOAPCrossZLevelTargetProvider> args)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var ourWorldPos = _transform.GetWorldPosition(xform);
        var currentMapUid = xform.MapUid;
        if (currentMapUid == null)
            return;

        //First - search current map
        var found = HorizontalSearch(ent, ourWorldPos, currentMapUid.Value, args.Provider.VisionRadius, false);

        if (found is not null)
        {
            args.TargetEntity = found;
            if (_xformQuery.TryGetComponent(args.TargetEntity, out var targetXform))
                args.TargetCoordinates = targetXform.Coordinates;
            return;
        }

        //Second - search map below and filter targets with transparent tile above their head
        if (_zLevels.TryMapDown(currentMapUid.Value, out var mapBelow))
        {
            found = HorizontalSearch(ent, ourWorldPos, mapBelow.Value, args.Provider.VisionRadius, true);

            if (found is not null)
            {
                args.TargetEntity = found;
                if (_xformQuery.TryGetComponent(args.TargetEntity, out var targetXform))
                    args.TargetCoordinates = targetXform.Coordinates;
                return;
            }
        }

        //Third - if we have transparent tile above, search map above.
        if (IsTileTransparentAt(currentMapUid.Value, ourWorldPos))
        {
            if (_zLevels.TryMapUp(currentMapUid.Value, out var mapAbove))
            {
                found = HorizontalSearch(ent, ourWorldPos, mapAbove.Value, args.Provider.VisionRadius, true);

                if (found is not null)
                {
                    args.TargetEntity = found;
                    if (_xformQuery.TryGetComponent(args.TargetEntity, out var targetXform))
                        args.TargetCoordinates = targetXform.Coordinates;
                    return;
                }
            }
        }
    }

    private EntityUid? HorizontalSearch(EntityUid ent, Vector2 entWorldPos, EntityUid searchMapUid, float range, bool filterEmptyTileAbove)
    {
        if (!_mapQuery.TryComp(searchMapUid, out var mapComp))
            return null;

        var searchMapId = mapComp.MapId;
        var checkMapPosition = new MapCoordinates(entWorldPos, searchMapId);

        // Find faction members on this map within range
        foreach (var found in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(checkMapPosition, range))
        {
            if (found.Owner == ent)
                continue;

            // Check hostility
            if (!IsHostile(ent, found))
                continue;

            if (!_xformQuery.TryGetComponent(found, out var targetXform))
                continue;

            var targetWorldPos = _transform.GetWorldPosition(targetXform);
            var dist = Vector2.Distance(entWorldPos, targetWorldPos);

            if (dist >= range)
                continue;

            if (filterEmptyTileAbove && !IsTileTransparentAt(searchMapUid, targetWorldPos))
                continue;

            // LOS check
            var pos2 = new MapCoordinates(targetWorldPos, searchMapId);
            if (!_examine.InRangeUnOccluded(checkMapPosition, pos2, range + 0.5f, null))
                continue;

            return found;
        }

        return null;
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
    /// </summary>
    private bool IsHostile(EntityUid npc, Entity<NpcFactionMemberComponent> target)
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
