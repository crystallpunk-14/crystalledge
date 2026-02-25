using Content.Shared._CE.Animation.Core.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._CE.Animation.Core;

public abstract partial class CESharedAnimationActionSystem
{
    private void InitMovementSpeed()
    {
        SubscribeLocalEvent<CEActiveAnimationActionComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnRefreshMovementSpeedModifiers(Entity<CEActiveAnimationActionComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!_proto.Resolve(ent.Comp.ActiveAnimation, out var animation))
            return;

        args.ModifySpeed(animation.MovementSpeed);
    }
}
