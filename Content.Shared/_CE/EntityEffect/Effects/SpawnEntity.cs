using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnEntity : CEEntityEffectBase<SpawnEntity>
{
    [DataField]
    public List<EntProtoId> Spawns = new();

    [DataField]
    public bool Reparent;
}

public sealed partial class CESpawnEntityEffectSystem : CEEntityEffectSystem<SpawnEntity>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void Effect(ref CEEntityEffectEvent<SpawnEntity> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var coords))
            return;

        if (_net.IsClient)
            return;

        foreach (var spawn in args.Effect.Spawns)
        {
            var spawned = SpawnAtPosition(spawn, coords);

            if (args.Effect.Reparent && args.Args.Target != null)
                _transform.SetParent(spawned, args.Args.Target.Value);
        }
    }
}
