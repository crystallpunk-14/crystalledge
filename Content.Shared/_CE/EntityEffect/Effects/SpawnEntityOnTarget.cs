using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnEntityOnTarget : CEEntityEffectBase<SpawnEntityOnTarget>
{
    [DataField]
    public List<EntProtoId> Spawns = new();
}

public sealed partial class CESpawnEntityOnTargetEffectSystem : CEEntityEffectSystem<SpawnEntityOnTarget>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(ref CEEntityEffectEvent<SpawnEntityOnTarget> args)
    {
        if (!TryResolveTargetCoordinates(args.Args, out var targetPoint))
            return;

        if (_net.IsClient)
            return;

        foreach (var spawn in args.Effect.Spawns)
        {
            EntityManager.SpawnAtPosition(spawn, targetPoint);
        }
    }
}
