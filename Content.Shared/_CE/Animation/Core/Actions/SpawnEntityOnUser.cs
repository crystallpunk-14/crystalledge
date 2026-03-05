using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class SpawnEntityOnUser : CEAnimationActionEntry
{
    [DataField]
    public List<EntProtoId> Spawns = new();

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
        if (!entManager.TryGetComponent<TransformComponent>(user, out var transformComponent))
            return;

        var netMan = IoCManager.Resolve<INetManager>();
        if (netMan.IsClient)
            return;

        foreach (var spawn in Spawns)
        {
            entManager.SpawnAtPosition(spawn, transformComponent.Coordinates);
        }
    }
}
