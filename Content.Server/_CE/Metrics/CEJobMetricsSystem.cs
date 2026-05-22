using System.Diagnostics.Metrics;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Roles;
using Robust.Server.DataMetrics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Metrics;

public sealed class CEJobMetricsSystem : EntitySystem
{
    [Dependency] private readonly IMetricsManager _metrics = default!;
    [Dependency] private readonly IMeterFactory _meterFactory = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly JobSystem _job = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private Dictionary<ProtoId<JobPrototype>, int> _activeJobs = new();

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("edge.metrics");

        _metrics.UpdateMetrics += MeasureRoleCounts;

        var meter = _meterFactory.Create("CrystallEdge.JobMetrics");

        meter.CreateObservableGauge(
            "job_roles_active_players",
            MeasureJobCount,
            null,
            "A counter showing the number of game roles currently being played");
    }

    private IEnumerable<Measurement<int>> MeasureJobCount()
    {
        if (_activeJobs.Count == 0)
            yield break;

        foreach (var (jobProto, playerCount) in _activeJobs)
        {
            yield return new Measurement<int>(
                playerCount,
                new KeyValuePair<string, object?>("job", jobProto)
            );
        }
    }

    private void MeasureRoleCounts()
    {
        _sawmill.Verbose("Updating jobs metrics");

        _activeJobs.Clear();

        var allJobs = _proto.EnumeratePrototypes<JobPrototype>();
        foreach (var job in allJobs)
        {
            if (!job.SetPreference)
                continue;

            _activeJobs.Add(job, 0);
        }

        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out var actor))
        {
            if (!_mind.TryGetMind(actor.PlayerSession.UserId, out var mind))
                continue;

            if (!_job.MindTryGetJob(mind, out var jobProto))
                continue;

            if (!jobProto.SetPreference)
                continue;

            if (!_activeJobs.TryGetValue(jobProto, out var activeCount))
                continue;

            _activeJobs[jobProto] = activeCount + 1;
        }
    }
}
