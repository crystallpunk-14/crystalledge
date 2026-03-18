using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.CPUJob.JobQueues;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// A simple <see cref="Job{T}"/> wrapper that executes a synchronous delegate.
/// Used by lightweight generators (e.g. static map, static z-network) that
/// complete their work in a single frame without cooperative yielding.
/// </summary>
public sealed class CEDelegateDungeonJob : Job<CEDungeonGenerateResult>
{
    private readonly Func<CEDungeonGenerateResult> _work;

    public CEDelegateDungeonJob(
        double maxTime,
        Func<CEDungeonGenerateResult> work,
        CancellationToken cancellation = default)
        : base(maxTime, cancellation)
    {
        _work = work;
    }

    protected override Task<CEDungeonGenerateResult> Process()
    {
        var result = _work();
        return Task.FromResult(result);
    }
}
