using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnEntityOnUser : CEEntityEffectBase<SpawnEntityOnUser>
{
    [DataField]
    public List<EntProtoId> Spawns = new();
}

public sealed partial class CESpawnEntityOnUserEffectSystem : CEEntityEffectSystem<SpawnEntityOnUser>
{
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(ref CEEntityEffectEvent<SpawnEntityOnUser> args)
    {
        var transformComponent = Transform(args.Args.User);

        if (_net.IsClient)
            return;

        foreach (var spawn in args.Effect.Spawns)
        {
            EntityManager.SpawnAtPosition(spawn, transformComponent.Coordinates);
        }
    }
}
