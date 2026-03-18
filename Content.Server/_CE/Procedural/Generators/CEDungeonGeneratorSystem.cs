using System.Threading;
using Robust.Shared.CPUJob.JobQueues;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Abstract base system for handling a specific <see cref="CEDungeonGeneratorConfigBase{T}"/>.
/// Each concrete generator system subscribes to <see cref="CEDungeonGenerateEvent{TConfig}"/>
/// and implements the <see cref="CreateJob"/> method to produce a cooperative job.
/// </summary>
/// <typeparam name="TConfig">The concrete generator config type this system handles.</typeparam>
public abstract partial class CEDungeonGeneratorSystem<TConfig> : EntitySystem
    where TConfig : CEDungeonGeneratorConfigBase<TConfig>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDungeonGenerateEvent<TConfig>>(OnGenerate);
    }

    private void OnGenerate(ref CEDungeonGenerateEvent<TConfig> args)
    {
        args.Job = CreateJob(args.Config, args.MaxTime, args.Cancellation);
    }

    /// <summary>
    /// Create a <see cref="Job{T}"/> that performs the actual generation logic for this config type.
    /// The job will be enqueued onto the <see cref="CEDungeonSystem"/> job queue.
    /// For simple generators, use <see cref="CEDelegateDungeonJob"/> to wrap synchronous work.
    /// For heavy generators, create a dedicated Job subclass with cooperative yielding.
    /// </summary>
    protected abstract Job<CEDungeonGenerateResult> CreateJob(
        TConfig config,
        double maxTime,
        CancellationToken cancellation);
}
