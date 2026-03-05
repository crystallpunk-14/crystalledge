using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class SpawnInHandEntity : CEAnimationActionEntry
{
    [DataField]
    public List<EntProtoId> Spawns = new();

    [DataField]
    public bool DeleteIfCantPickup;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        if (target is null)
            return;

        if (!entManager.TryGetComponent<TransformComponent>(target.Value, out var transformComponent))
            return;

        var netMan = IoCManager.Resolve<INetManager>();
        if (netMan.IsClient)
            return;

        var handSystem = entManager.System<SharedHandsSystem>();

        foreach (var spawn in Spawns)
        {
            var item = entManager.SpawnAtPosition(spawn, transformComponent.Coordinates);
            if (!handSystem.TryPickupAnyHand(target.Value, item) && DeleteIfCantPickup)
                entManager.QueueDeleteEntity(item);
        }
    }
}
