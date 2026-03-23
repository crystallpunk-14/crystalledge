using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.Procedural.Instance;

/// <summary>
/// Manages dungeon level instances: creation, player routing, entry/exit lifecycle, and cleanup.
/// <list type="bullet">
///   <item>Stable levels exist as singletons — one instance per server, recreated if deleted.</item>
///   <item>Unstable levels can have multiple instances; new groups get a fresh instance or join
///         an existing one that still has active entry points.</item>
/// </list>
/// </summary>
public sealed partial class CEDungeonInstanceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>
    /// How long an empty unstable instance persists before cleanup.
    /// </summary>
    private static readonly TimeSpan UnstableCleanupDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the cleanup check runs (avoid per-frame iteration).
    /// </summary>
    private static readonly TimeSpan CleanupCheckInterval = TimeSpan.FromSeconds(20);
    private TimeSpan _nextCleanupCheck;

    private EntityQuery<CEDungeonInstanceComponent> _instanceQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetQuery;

    public override void Initialize()
    {
        base.Initialize();

        _instanceQuery = GetEntityQuery<CEDungeonInstanceComponent>();
        _zNetQuery = GetEntityQuery<CEZLevelsNetworkComponent>();

        InitializePassage();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdatePassage();

        if (_timing.CurTime < _nextCleanupCheck)
            return;

        _nextCleanupCheck = _timing.CurTime + CleanupCheckInterval;
        UpdateCleanup(_timing.CurTime);
    }

    /// <summary>
    /// Builds a set of MapIds that currently have living players, then checks unstable instances
    /// for emptiness and deletes them after the cleanup delay.
    /// </summary>
    private void UpdateCleanup(TimeSpan curTime)
    {
        // Build a set of MapIds with at least one living player.
        var occupiedMaps = new HashSet<MapId>();
        var mobQuery = EntityQueryEnumerator<CEDungeonPlayerComponent, CEMobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out _, out _, out var mobState, out var xform))
        {
            occupiedMaps.Add(xform.MapID);
        }

        var query = EntityQueryEnumerator<CEDungeonInstanceComponent>();
        while (query.MoveNext(out var uid, out var inst))
        {
            if (inst.Stable)
                continue;

            var mapIds = GetInstanceMapIds(uid);
            var hasPlayers = false;
            foreach (var mapId in mapIds)
            {
                if (occupiedMaps.Contains(mapId))
                {
                    hasPlayers = true;
                    break;
                }
            }

            if (hasPlayers)
            {
                inst.EmptySince = null;
                continue;
            }

            inst.EmptySince ??= curTime;

            if (curTime - inst.EmptySince.Value < UnstableCleanupDelay)
                continue;

            Log.Info($"cleaning up empty unstable instance '{inst.PrototypeId}'.");
            _zLevels.DeleteZNetwork(uid);
        }
    }
}
