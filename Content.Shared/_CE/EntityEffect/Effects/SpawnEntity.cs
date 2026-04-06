using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnEntity : CEEntityEffectBase<SpawnEntity>
{
    [DataField]
    public List<EntProtoId> Spawns = new();
}

public sealed partial class CESpawnEntityEffectSystem : CEEntityEffectSystem<SpawnEntity>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(ref CEEntityEffectEvent<SpawnEntity> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var coords))
            return;

        if (_net.IsClient)
            return;

        foreach (var spawn in args.Effect.Spawns)
        {
            SpawnAtPosition(spawn, coords);
        }
    }
}
