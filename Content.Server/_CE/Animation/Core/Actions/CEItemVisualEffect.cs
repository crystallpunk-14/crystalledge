using Content.Shared._CE.Animation.Core.Actions;

namespace Content.Server._CE.Animation.Core.Actions;

public sealed partial class CEItemVisualEffect : CESharedItemVisualEffect
{
    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle)
    {
        //Do nothing on server
    }
}
