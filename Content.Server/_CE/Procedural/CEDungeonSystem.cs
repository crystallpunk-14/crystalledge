using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.PostProcess;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Server.Decals;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural;

public sealed partial class CEDungeonSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly CEDungeonPostProcessSystem _postProcess = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<(Vector2i, Tile)> _tiles = new();

    public static readonly ProtoId<ContentTileDefinition> FallbackTileId = "CEStone";

    /// <summary>
    /// Maximum time (seconds) the job queue is allowed to run per frame.
    /// </summary>
    private const double DungeonJobTime = 0.002;

    private readonly JobQueue _dungeonJobQueue = new(DungeonJobTime);
    private readonly Dictionary<Job<CEDungeonGenerateResult>, CancellationTokenSource> _dungeonJobs = new();

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        InitializeRooms();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _dungeonJobQueue.Process();
    }

    /// <summary>
    /// Drives the dungeon job queue forward by one slice. Used to synchronously
    /// pump pending generations to completion (e.g. during round-start) without
    /// waiting for the next frame's <see cref="Update"/>.
    /// </summary>
    public void ProcessJobs() => _dungeonJobQueue.Process();

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var cts in _dungeonJobs.Values)
        {
            cts.Cancel();
        }

        _dungeonJobs.Clear();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CEDungeonRoom3DPrototype>())
            InvalidateRoomPasswayCache();
    }

    /// <summary>
    /// Generates a dungeon level asynchronously. The work runs cooperatively
    /// across multiple frames via the internal <see cref="JobQueue"/>.
    /// </summary>
    public async Task<CEDungeonGenerateResult> GenerateLevelAsync(ProtoId<CEDungeonLevelPrototype> protoId)
    {
        if (!_proto.TryIndex(protoId, out var proto))
        {
            Log.Error($"CEDungeonSystem: unknown dungeon level prototype '{protoId}'.");
            return new CEDungeonGenerateResult(false);
        }

        return await GenerateLevelAsync(proto);
    }

    /// <summary>
    /// Generates a dungeon level asynchronously from the given prototype.
    /// </summary>
    public async Task<CEDungeonGenerateResult> GenerateLevelAsync(CEDungeonLevelPrototype proto)
    {
        var cts = new CancellationTokenSource();
        var job = proto.Config.CreateJob(EntityManager, DungeonJobTime, cts.Token);

        if (job == null)
        {
            Log.Error($"CEDungeonSystem: no generator handled config for dungeon level '{proto.ID}'.");
            return new CEDungeonGenerateResult(false);
        }

        _dungeonJobs[job] = cts;
        _dungeonJobQueue.EnqueueJob(job);
        await job.AsTask;
        _dungeonJobs.Remove(job);

        if (job.Exception != null)
        {
            Log.Error($"CEDungeonSystem: generation failed for dungeon level '{proto.ID}': {job.Exception}");
            throw job.Exception;
        }

        var result = job.Result;

        if (result is { Success: true, MapUid: not null })
        {
            // Initialize z-network maps now that we are outside the job context,
            // so InitializeMap does not conflict with PVS parallel jobs.
            if (result.ZNetworkUid != null
                && TryComp<CEZLevelsNetworkComponent>(result.ZNetworkUid.Value, out var networkComp))
            {
                _zLevels.InitializeZNetwork((result.ZNetworkUid.Value, networkComp));
            }

            NameDungeonResult(proto, result);
            Log.Info($"CEDungeonSystem: generated dungeon level '{proto.ID}' on map {result.MapId}.");

            // Run post-processing layers.
            if (proto.PostProcess.Count > 0)
            {
                var ppJob = new CEDungeonPostProcessJob(
                    DungeonJobTime, _postProcess, proto.PostProcess, result.MapUid.Value, proto.MainZLevel, cts.Token);
                _dungeonJobQueue.EnqueueJob(ppJob);
                await ppJob.AsTask;

                if (ppJob.Exception != null)
                    Log.Error($"CEDungeonSystem: post-processing failed for '{proto.ID}': {ppJob.Exception}");
                else
                    Log.Info($"CEDungeonSystem: post-processing complete for '{proto.ID}'.");
            }
        }
        else
        {
            Log.Error($"CEDungeonSystem: generation failed for dungeon level '{proto.ID}'.");
        }

        return result;
    }

    /// <summary>
    /// Fire-and-forget dungeon generation. Enqueues the job and returns immediately.
    /// Results are logged when the job completes.
    /// </summary>
    public void GenerateLevel(ProtoId<CEDungeonLevelPrototype> protoId)
    {
        if (!_proto.TryIndex(protoId, out var proto))
        {
            Log.Error($"CEDungeonSystem: unknown dungeon level prototype '{protoId}'.");
            return;
        }

        GenerateLevel(proto);
    }

    /// <summary>
    /// Fire-and-forget dungeon generation from a prototype.
    /// </summary>
    public void GenerateLevel(CEDungeonLevelPrototype proto)
    {
        var cts = new CancellationTokenSource();
        var job = proto.Config.CreateJob(EntityManager, DungeonJobTime, cts.Token);

        if (job == null)
        {
            Log.Error($"CEDungeonSystem: no generator handled config for dungeon level '{proto.ID}'.");
            return;
        }

        _dungeonJobs[job] = cts;
        _dungeonJobQueue.EnqueueJob(job);

        // Log result when the job completes (fire-and-forget).
        var protoId = proto.ID;
        job.AsTask.ContinueWith(_ =>
        {
            _dungeonJobs.Remove(job);

            if (job.Exception != null)
            {
                Log.Error($"CEDungeonSystem: generation failed for '{protoId}': {job.Exception}");
                return;
            }

            var result = job.Result;
            if (result is { Success: true, MapUid: not null })
            {
                // Initialize z-network maps now that we are outside the job context.
                if (result.ZNetworkUid != null
                    && TryComp<CEZLevelsNetworkComponent>(result.ZNetworkUid.Value, out var networkComp))
                {
                    _zLevels.InitializeZNetwork((result.ZNetworkUid.Value, networkComp));
                }

                NameDungeonResult(proto, result);
                Log.Info($"CEDungeonSystem: generated dungeon level '{protoId}' on map {result.MapId}.");

                // Enqueue post-processing layers.
                if (proto.PostProcess.Count > 0)
                {
                    var ppJob = new CEDungeonPostProcessJob(
                        DungeonJobTime, _postProcess, proto.PostProcess, result.MapUid.Value, proto.MainZLevel, cts.Token);
                    _dungeonJobQueue.EnqueueJob(ppJob);
                }
            }
            else
            {
                Log.Error($"CEDungeonSystem: generation failed for '{protoId}'.");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Renames the freshly generated dungeon's z-network entity (if any) and all of its maps,
    /// matching the convention used by the station / mapping z-network commands. Falls back to
    /// renaming just the primary map when the level wasn't created inside a z-network.
    /// </summary>
    private void NameDungeonResult(CEDungeonLevelPrototype proto, CEDungeonGenerateResult result)
    {
        if (result.MapUid is not { } mapUid)
            return;

        var dungeonName = proto.Name.HasValue ? Loc.GetString(proto.Name.Value) : proto.ID;

        if (result.ZNetworkUid is { } networkUid
            && TryComp<CEZLevelsNetworkComponent>(networkUid, out var networkComp))
        {
            // Unstable levels can have multiple concurrent instances — disambiguate with the
            // network entity id so admins can tell them apart in dev tools / overview UIs.
            var mapNameBase = proto.Stable ? dungeonName : $"{dungeonName} #{networkUid.Id}";

            _zLevels.SetZNetworkName(
                (networkUid, networkComp),
                $"Dungeon z-Network: {mapNameBase}",
                mapNameBase);
        }
        else
        {
            _meta.SetEntityName(mapUid, proto.Stable ? dungeonName : $"{dungeonName} #{mapUid.Id}");
        }
    }
}
