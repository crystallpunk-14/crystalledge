using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Actions;
using Robust.Shared.Player;

namespace Content.Server._CE.Animation.Core.Actions;

public sealed partial class ItemVisualEffect : SharedItemVisualEffect
{
    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, TimeSpan frame)
    {
        // Server sends visual effect event to all non-predicting clients
        var filter = Filter.PvsExcept(entity, entityManager: entManager);
        var effectEvent = new CEItemVisualEffectEvent(
            entManager.GetNetEntity(entity),
            used.HasValue ? entManager.GetNetEntity(used.Value) : null,
            angle,
            frame);

        foreach (var session in filter.Recipients)
        {
            entManager.EntityNetManager.SendSystemNetworkMessage(effectEvent, session.Channel);
        }
    }
}
