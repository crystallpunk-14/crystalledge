using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnInHandEntity : CEEntityEffectBase<SpawnInHandEntity>
{
    [DataField]
    public List<EntProtoId> Spawns = new();

    [DataField]
    public bool DeleteIfCantPickup;
}

public sealed partial class CESpawnInHandEntityEffectSystem : CEEntityEffectSystem<SpawnInHandEntity>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    protected override void Effect(ref CEEntityEffectEvent<SpawnInHandEntity> args)
    {
        if (args.Args.Target is null)
            return;

        var transformComponent = Transform(args.Args.Target.Value);

        if (_net.IsClient)
            return;

        foreach (var spawn in args.Effect.Spawns)
        {
            var item = EntityManager.SpawnAtPosition(spawn, transformComponent.Coordinates);
            if (!_hands.TryPickupAnyHand(args.Args.Target.Value, item) && args.Effect.DeleteIfCantPickup)
                EntityManager.QueueDeleteEntity(item);
        }
    }
}
