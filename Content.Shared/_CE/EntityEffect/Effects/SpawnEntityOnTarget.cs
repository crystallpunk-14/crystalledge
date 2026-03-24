using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class SpawnEntityOnTarget : CEEntityEffect
{
    [DataField]
    public List<EntProtoId> Spawns = new();

    public override void Effect(
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
