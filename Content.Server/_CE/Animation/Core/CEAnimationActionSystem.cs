using System.Linq;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;

namespace Content.Server._CE.Animation.Core;

public sealed partial class CEAnimationActionSystem : CESharedAnimationActionSystem
{
    protected override void OnKeyFrameProcessed(
        EntityUid uid,
        EntityUid? used,
        Angle angle,
        TimeSpan keyFrame,
        List<CEEntityEffect> actions)
    {
        // Send visual sync event to non-predicting clients if this keyframe has visual effects
        if (!actions.Any(a => a is EntityAnimation))
            return;

        var filter = CEFilter.ZPvsExcept(uid, EntityManager);
        var effectEvent = new CEEntityAnimationEvent(
            GetNetEntity(uid),
            used.HasValue ? GetNetEntity(used.Value) : null,
            angle,
            keyFrame);

        foreach (var session in filter.Recipients)
        {
            EntityManager.EntityNetManager!.SendSystemNetworkMessage(effectEvent, session.Channel);
        }
    }
}
