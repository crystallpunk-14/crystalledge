using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class SpawnEntityOnTarget : CEAnimationActionEntry
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
        EntityCoordinates? targetPoint = null;
        if (position is not null)
            targetPoint = position.Value;
        if (target is not null && entManager.TryGetComponent<TransformComponent>(target.Value, out var transformComponent))
            targetPoint = transformComponent.Coordinates;

        if (targetPoint is null)
            return;

        var netMan = IoCManager.Resolve<INetManager>();
        if (netMan.IsClient)
            return;

        foreach (var spawn in Spawns)
        {
            entManager.SpawnAtPosition(spawn, targetPoint.Value);
        }
    }
}
