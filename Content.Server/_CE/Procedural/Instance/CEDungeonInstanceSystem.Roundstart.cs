using Content.Server._CE.Procedural.Prototypes;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    private void InitializeRoundstart()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    /// <summary>
    /// Generates and registers every <see cref="CEDungeonLevelPrototype"/> with
    /// <c>roundstart: true</c> as a singleton instance, so the round can begin with
    /// dungeon levels already present (their embedded spawn points pick up players
    /// during the standard <c>SpawnPlayers</c> step right after this event).
    /// The job queue is pumped synchronously so generation finishes before player spawn.
    /// </summary>
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        foreach (var proto in _proto.EnumeratePrototypes<CEDungeonLevelPrototype>())
        {
            if (proto.Abstract || !proto.Roundstart)
                continue;

            var task = _dungeon.GenerateLevelAsync(proto);

            // Pump the dungeon job queue synchronously so generation completes before
            // player spawn happens. Static z-network configs finish in a few ticks; even
            // worst-case generators only block the main thread briefly during round start.
            while (!task.IsCompleted)
            {
                _dungeon.ProcessJobs();
            }

            if (task.IsFaulted || !task.IsCompletedSuccessfully)
            {
                Log.Error($"Round-start dungeon generation failed for '{proto.ID}': {task.Exception}");
                continue;
            }

            var result = task.GetAwaiter().GetResult();
            if (!result.Success || result.MapUid == null)
            {
                Log.Error($"Round-start dungeon generation produced no map for '{proto.ID}'.");
                continue;
            }

            RegisterInstance(result.MapUid.Value, proto);
            Log.Info($"Registered round-start dungeon instance '{proto.ID}'.");
        }
    }
}
