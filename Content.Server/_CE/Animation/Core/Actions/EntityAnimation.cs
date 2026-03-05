using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Actions;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._CE.Animation.Core.Actions;

public sealed partial class EntityAnimation : SharedEntityAnimation
{
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
        // Server sends visual effect event to all non-predicting clients
        var filter = Filter.PvsExcept(user, entityManager: entManager);
        var effectEvent = new CEEntityAnimationEvent(
            entManager.GetNetEntity(user),
            used.HasValue ? entManager.GetNetEntity(used.Value) : null,
            angle,
            frame);

        foreach (var session in filter.Recipients)
        {
            entManager.EntityNetManager.SendSystemNetworkMessage(effectEvent, session.Channel);
        }
    }
}
