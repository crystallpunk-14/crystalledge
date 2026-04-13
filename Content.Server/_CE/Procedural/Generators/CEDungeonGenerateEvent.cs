using System.Threading;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Raised as a broadcast event when a dungeon level needs to be generated.
/// The strongly-typed config <typeparamref name="T"/> lets the matching
/// <see cref="CEDungeonGeneratorSystem{T}"/> pick it up via ECS event subscription.
/// </summary>
/// <remarks>
/// Handlers should create a <see cref="Job{T}"/> and assign it to <see cref="Job"/>.
/// The job will be enqueued onto the <see cref="CEDungeonSystem"/> job queue
/// and executed cooperatively across frames.
/// </remarks>
[ByRefEvent]
public record struct CEDungeonGenerateEvent<T>(T Config, double MaxTime, CancellationToken Cancellation)
    where T : CEDungeonGeneratorConfigBase<T>
{
    /// <summary>
    /// Set by the handler: the job that will perform the actual generation work.
    /// </summary>
    public Job<CEDungeonGenerateResult>? Job;
}
