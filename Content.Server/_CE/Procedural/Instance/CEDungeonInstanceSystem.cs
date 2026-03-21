using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Player;
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

    private EntityQuery<CEDungeonInstanceComponent> _instanceQuery;
    private EntityQuery<CEDungeonLevelEntryComponent> _entryQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetQuery;

    /// <summary>
    /// Tracks in-flight generation tasks keyed by the exit entity that initiated them.
    /// </summary>
    private readonly Dictionary<EntityUid, Task<CEDungeonGenerateResult>> _pendingGenerations = new();

    public override void Initialize()
    {
        base.Initialize();

        _instanceQuery = GetEntityQuery<CEDungeonInstanceComponent>();
        _entryQuery = GetEntityQuery<CEDungeonLevelEntryComponent>();
        _zNetQuery = GetEntityQuery<CEZLevelsNetworkComponent>();

        InitializeExit();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateCleanup(_timing.CurTime);
    }

    /// <summary>
    /// Remove empty unstable instances past the cleanup delay.
    /// </summary>
    private void UpdateCleanup(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<CEDungeonInstanceComponent>();
        while (query.MoveNext(out var uid, out var inst))
        {
            if (inst.Stable)
                continue;

            if (inst.PlayerCount > 0)
                continue;

            if (curTime - inst.CreatedAt < UnstableCleanupDelay)
                continue;

            Log.Info($"CEDungeonInstanceSystem: cleaning up empty unstable instance '{inst.PrototypeId}'.");
            DeleteInstance(uid);
        }
    }
}
