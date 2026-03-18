using System.Threading;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Abstract base for all dungeon generator configurations.
/// Concrete configs define data for a specific generation strategy (static map, procedural, etc.)
/// and raise typed events so the matching <see cref="CEDungeonGeneratorSystem{TConfig}"/> can
/// create a <see cref="Job{T}"/> for cooperative execution.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonGeneratorConfig
{
    /// <summary>
    /// Raises a <see cref="CEDungeonGenerateEvent{T}"/> to dispatch job creation to the correct
    /// handler system. Returns a <see cref="Job{T}"/> that performs the actual generation.
    /// </summary>
    public abstract Job<CEDungeonGenerateResult>? CreateJob(
        IEntityManager entMan,
        double maxTime,
        CancellationToken cancellation);
}

/// <summary>
/// Result returned by dungeon generation jobs.
/// Contains only the primary map created by the generator.
/// Any generator-specific data (z-networks, grids, etc.) should be managed internally by the generator system.
/// </summary>
public record struct CEDungeonGenerateResult(
    bool Success,
    EntityUid? MapUid = null,
    MapId? MapId = null);

public abstract partial class CEDungeonGeneratorConfigBase<T> : CEDungeonGeneratorConfig
    where T : CEDungeonGeneratorConfigBase<T>
{
    public override Job<CEDungeonGenerateResult>? CreateJob(
        IEntityManager entMan,
        double maxTime,
        CancellationToken cancellation)
    {
        if (this is not T typed)
            return null;

        var ev = new CEDungeonGenerateEvent<T>(typed, maxTime, cancellation);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);

        return ev.Job;
    }
}
