using Content.Server._CE.Boss;
using Content.Shared._CE.Music;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._CE.Music;

/// <summary>
/// Server-side controller that flips <see cref="CEMapBossMusicComponent.State"/> on a boss map
/// when the boss battle starts or ends. The networked state is consumed by the client to play
/// the matching layer of the boss soundtrack.
/// If the affected map is part of a Z-level network, the state is propagated to every
/// <see cref="CEMapBossMusicComponent"/> on every map in that network so the soundtrack stays
/// in sync no matter which z-level the local player is on.
/// </summary>
public sealed class CEBossMusicSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    private readonly HashSet<EntityUid> _affectedMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBossBattleStartedEvent>(OnBattleStarted);
        SubscribeLocalEvent<CEBossBattleEndedEvent>(OnBattleEnded);
    }

    private void OnBattleStarted(CEBossBattleStartedEvent ev)
    {
        SetStateOnMap(ev.MapId, CEBossMusicState.Battle);
    }

    private void OnBattleEnded(CEBossBattleEndedEvent ev)
    {
        SetStateOnMap(ev.MapId, CEBossMusicState.Victory);
    }

    private void SetStateOnMap(MapId mapId, CEBossMusicState state)
    {
        if (mapId == MapId.Nullspace)
            return;

        CollectAffectedMaps(mapId);
        if (_affectedMaps.Count == 0)
            return;

        var query = EntityQueryEnumerator<CEMapBossMusicComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_affectedMaps.Contains(uid))
                continue;

            if (comp.State == state)
                continue;

            comp.State = state;
            Dirty(uid, comp);
        }
    }

    private void CollectAffectedMaps(MapId mapId)
    {
        _affectedMaps.Clear();

        if (!_mapManager.MapExists(mapId))
            return;

        var sourceUid = _mapManager.GetMapEntityId(mapId);
        if (!sourceUid.IsValid())
            return;

        _affectedMaps.Add(sourceUid);

        if (!_zLevels.TryGetZNetwork(sourceUid, out var network))
            return;

        foreach (var levelUid in network.Comp.SortedZLevels)
        {
            if (levelUid.IsValid())
                _affectedMaps.Add(levelUid);
        }
    }
}
