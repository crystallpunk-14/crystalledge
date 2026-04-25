using System.Linq;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Overview;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural.Admin;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Admin;

/// <summary>
/// Serves state for the admin dungeon overview UI on the <see cref="CEAdminDungeonOverviewUiKey.Key"/> interface.
/// State is refreshed whenever the BUI opens. Handles teleport-to-player requests.
/// </summary>
public sealed class CEAdminDungeonOverviewSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    private EntityQuery<CEDungeonInstanceComponent> _instanceQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetQuery;

    public override void Initialize()
    {
        base.Initialize();

        _instanceQuery = GetEntityQuery<CEDungeonInstanceComponent>();
        _zNetQuery = GetEntityQuery<CEZLevelsNetworkComponent>();

        SubscribeLocalEvent<CEAdminDungeonOverviewComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CEAdminDungeonOverviewComponent, CEAdminDungeonOverviewTeleportMsg>(OnTeleport);
        SubscribeLocalEvent<CEDungeonPlayerLevelChangedEvent>(OnPlayerLevelChanged);
    }

    private void OnPlayerLevelChanged(ref CEDungeonPlayerLevelChangedEvent ev)
    {
        var query = EntityQueryEnumerator<CEAdminDungeonOverviewComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CEAdminDungeonOverviewUiKey.Key))
                RefreshState(uid);
        }
    }

    private void OnUiOpened(Entity<CEAdminDungeonOverviewComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, CEAdminDungeonOverviewUiKey.Key))
            return;

        RefreshState(ent.Owner);
    }

    private void OnTeleport(Entity<CEAdminDungeonOverviewComponent> ent, ref CEAdminDungeonOverviewTeleportMsg args)
    {
        if (!TryGetEntity(args.Target, out var target))
            return;

        _transform.SetCoordinates(ent.Owner, Transform(target.Value).Coordinates);
        _transform.AttachToGridOrMap(ent.Owner);
    }

    private void RefreshState(EntityUid user)
    {
        // Group instances by level prototype id.
        var instancesByLevel = new Dictionary<string, List<CEAdminDungeonOverviewInstanceEntry>>();

        var instQuery = EntityQueryEnumerator<CEDungeonInstanceComponent>();
        while (instQuery.MoveNext(out var anchorUid, out var inst))
        {
            var entry = new CEAdminDungeonOverviewInstanceEntry
            {
                Anchor = GetNetEntity(anchorUid),
                Players = GetInstancePlayers(anchorUid),
            };

            if (!instancesByLevel.TryGetValue(inst.PrototypeId, out var list))
            {
                list = new List<CEAdminDungeonOverviewInstanceEntry>();
                instancesByLevel[inst.PrototypeId] = list;
            }

            list.Add(entry);
        }

        // Build level list from prototypes.
        var levels = new List<CEAdminDungeonOverviewLevelEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonLevelPrototype>())
        {
            if (proto.Abstract)
                continue;

            var entry = new CEAdminDungeonOverviewLevelEntry
            {
                Id = proto.ID,
                NameLocId = proto.Name,
                DescLocId = proto.Desc,
                UIPosition = proto.UIPosition,
                Icon = proto.Icon,
                Stable = proto.Stable,
                Exits = proto.Exits.Values.Select(v => v.Id).Distinct().ToList(),
            };

            if (instancesByLevel.TryGetValue(proto.ID, out var list))
                entry.Instances = list;

            levels.Add(entry);
        }

        var currentLevelId = TryGetCurrentLevelId(user);

        var state = new CEAdminDungeonOverviewState
        {
            Levels = levels,
            CurrentLevelId = currentLevelId,
        };

        _ui.SetUiState(user, CEAdminDungeonOverviewUiKey.Key, state);
    }

    private List<CEAdminDungeonOverviewPlayerEntry> GetInstancePlayers(EntityUid anchorUid)
    {
        var result = new List<CEAdminDungeonOverviewPlayerEntry>();

        var mapIds = GetInstanceMapIds(anchorUid);
        if (mapIds.Count == 0)
            return result;

        var query = EntityQueryEnumerator<CEDungeonPlayerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!mapIds.Contains(xform.MapID))
                continue;

            var entry = new CEAdminDungeonOverviewPlayerEntry
            {
                Entity = GetNetEntity(uid),
                CharacterName = Name(uid),
                AccountName = _playerManager.TryGetSessionByEntity(uid, out var session)
                    ? session.Name
                    : string.Empty,
            };

            result.Add(entry);
        }

        return result;
    }

    private HashSet<MapId> GetInstanceMapIds(EntityUid anchorUid)
    {
        var mapIds = new HashSet<MapId>();

        if (_zNetQuery.TryComp(anchorUid, out var zNet))
        {
            foreach (var (_, zMapUid) in zNet.ZLevels)
            {
                if (zMapUid != null && TryComp<MapComponent>(zMapUid.Value, out var mapComp))
                    mapIds.Add(mapComp.MapId);
            }
        }
        else if (TryComp<MapComponent>(anchorUid, out var anchorMap))
        {
            mapIds.Add(anchorMap.MapId);
        }

        return mapIds;
    }

    private string? TryGetCurrentLevelId(EntityUid user)
    {
        if (Transform(user).MapUid is not { } mapUid)
            return null;

        if (_zLevels.TryGetZNetwork(mapUid, out var zNetAnchor)
            && _instanceQuery.TryComp(zNetAnchor.Value.Owner, out var zInst))
        {
            return zInst.PrototypeId;
        }

        if (_instanceQuery.TryComp(mapUid, out var inst))
            return inst.PrototypeId;

        return null;
    }
}
