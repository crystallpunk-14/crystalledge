using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.CPUJob.JobQueues;

namespace Content.Server._CE.Procedural.PostProcess;

public sealed class CEDungeonPostProcessJob : Job<bool>
{
    private readonly CEDungeonPostProcessSystem _system;
    private readonly List<CEDungeonPostProcessLayer> _layers;
    private readonly EntityUid _mapUid;
    private readonly int _mainZLevel;

    public CEDungeonPostProcessJob(
        double maxTime,
        CEDungeonPostProcessSystem system,
        List<CEDungeonPostProcessLayer> layers,
        EntityUid mapUid,
        int mainZLevel,
        CancellationToken cancellation = default)
        : base(maxTime, cancellation)
    {
        _system = system;
        _layers = layers;
        _mapUid = mapUid;
        _mainZLevel = mainZLevel;
    }

    protected override async Task<bool> Process()
    {
        await _system.RunAll(_layers, _mapUid, _mainZLevel, SuspendIfOutOfTime);
        return true;
    }
}
