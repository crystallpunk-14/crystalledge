using Content.Shared._CE.DivineShield;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class AddDivineShield : CEAnimationActionEntry
{
    [DataField]
    public int Amount = 1;

    public override void Play(EntityManager entManager,
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

        var divine = entManager.System<CESharedDivineShieldSystem>();
        divine.TryAddShield(target.Value, Amount);
    }
}
