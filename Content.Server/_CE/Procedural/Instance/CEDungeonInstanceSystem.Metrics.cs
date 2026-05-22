using Content.Shared._CE.Procedural.Components;
using Prometheus;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    /// <summary>
    /// Records the time (in seconds since round start) when a player first arrives at each dungeon level.
    /// Buckets span 0–60 minutes in 1-minute steps, allowing Prometheus / Grafana bar charts
    /// showing the average time players take to reach any given level.
    /// </summary>
    private static readonly Histogram DungeonLevelReachSeconds = Prometheus.Metrics.CreateHistogram(
        "crystall_edge_dungeon_level_reach_seconds",
        "Seconds from round start until a player first reaches a dungeon level.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "level" },
            // 60 buckets: 60 s, 120 s, …, 3600 s (1 min – 60 min)
            Buckets = Histogram.LinearBuckets(start: 60, width: 60, count: 60),
        });

    private void TrackLevelReached(EntityUid player, string levelId)
    {
        if (!TryComp<CEDungeonPlayerComponent>(player, out var dungeonPlayer))
            return;

        var elapsed = (_timing.CurTime - dungeonPlayer.SessionStartedAt).TotalSeconds;
        if (elapsed < 0)
            return;

        DungeonLevelReachSeconds.WithLabels(levelId).Observe(elapsed);
    }
}
