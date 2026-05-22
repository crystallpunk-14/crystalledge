using System.Linq;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.Procedural.Overview;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Overview;

/// <summary>
/// Serves state for the player-facing dungeon overview UI on
/// <see cref="CEPlayerDungeonOverviewUiKey.Key"/>. Unlike the admin version this
/// only exposes per-level player counts — never names or entities.
/// </summary>
public sealed class CEPlayerDungeonOverviewSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    private EntityQuery<CEDungeonInstanceComponent> _instanceQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetQuery;

    public override void Initialize()
    {
        base.Initialize();

        _instanceQuery = GetEntityQuery<CEDungeonInstanceComponent>();
        _zNetQuery = GetEntityQuery<CEZLevelsNetworkComponent>();

        SubscribeLocalEvent<CEPlayerDungeonOverviewComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CEDungeonPlayerLevelChangedEvent>(OnPlayerLevelChanged);
    }

    private void OnPlayerLevelChanged(ref CEDungeonPlayerLevelChangedEvent ev)
    {
        // Re-push state to every host that currently has the overview UI open.
        var query = EntityQueryEnumerator<CEPlayerDungeonOverviewComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, CEPlayerDungeonOverviewUiKey.Key))
                RefreshState(uid);
        }
    }

    private void OnUiOpened(Entity<CEPlayerDungeonOverviewComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, CEPlayerDungeonOverviewUiKey.Key))
            return;

        RefreshState(ent.Owner);
    }

    private void RefreshState(EntityUid host)
    {
        // Aggregate player counts per level prototype id.
        var countsByLevel = new Dictionary<string, int>();

        var instQuery = EntityQueryEnumerator<CEDungeonInstanceComponent>();
        while (instQuery.MoveNext(out var anchorUid, out var inst))
        {
            var count = CountInstancePlayers(anchorUid);

            if (!countsByLevel.TryAdd(inst.PrototypeId, count))
                countsByLevel[inst.PrototypeId] += count;
        }

        var levels = new List<CEPlayerDungeonOverviewLevelEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonLevelPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (proto.Secret)
                continue;

            countsByLevel.TryGetValue(proto.ID, out var count);

            levels.Add(new CEPlayerDungeonOverviewLevelEntry
            {
                Id = proto.ID,
                NameLocId = proto.Name,
                DescLocId = proto.Desc,
                UIPosition = proto.UIPosition,
                Icon = proto.Icon,
                Stable = proto.Stable,
                Exits = proto.Exits.Values.Select(v => v.Id).Distinct().ToList(),
                PlayerCount = count,
            });
        }

        var state = new CEPlayerDungeonOverviewState
        {
            Levels = levels,
            CurrentLevelId = TryGetCurrentLevelId(host),
        };

        _ui.SetUiState(host, CEPlayerDungeonOverviewUiKey.Key, state);
    }

    private int CountInstancePlayers(EntityUid anchorUid)
    {
        var mapIds = GetInstanceMapIds(anchorUid);
        if (mapIds.Count == 0)
            return 0;

        var count = 0;
        var query = EntityQueryEnumerator<CEDungeonPlayerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (mapIds.Contains(xform.MapID))
                count++;
        }
        return count;
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

    private string? TryGetCurrentLevelId(EntityUid host)
    {
        if (Transform(host).MapUid is not { } mapUid)
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
