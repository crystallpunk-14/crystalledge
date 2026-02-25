using System.Linq;
using Content.Shared._CE.Animation.Core.Components;
using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Animation.Core;

public abstract partial class CESharedAnimationActionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitMovement();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEActiveAnimationActionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var controller, out var xform))
        {
            if (!_proto.Resolve(controller.ActiveAnimation, out var animation))
                continue;

            var animationEndTime = controller.StartAnimationTime + animation.Duration;

            //Finishing animation
            if (_timing.CurTime >= animationEndTime)
            {
                var finishedEv = new CEAnimationActionEndedEvent(animation, false);
                RaiseLocalEvent(uid, finishedEv);
                StopAnimation((uid, controller));
                continue;
            }

            //Processing animation events
            if (_timing.ApplyingState)
                continue; // Skip during state application to prevent duplicate events
                
            if (animation.Events.Any() && controller.StartAnimationTime.HasValue)
            {
                var startTime = controller.StartAnimationTime.Value;
                foreach (var (keyFrame, action) in animation.Events)
                {
                    // Skip events already processed
                    if (keyFrame <= controller.LastEvent)
                        continue;

                    var eventTime = startTime + keyFrame;
                    // Only trigger if event time is within this frame
                    if (eventTime > controller.LastEvent && eventTime <= _timing.CurTime)
                    {
                        action.Play(EntityManager, uid, controller.AnimationAngle ?? Angle.Zero);
                        controller.LastEvent = keyFrame;
                        Dirty(uid, controller); // Mark component dirty after state change
                    }
                }
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="animationProto"></param>
    /// <param name="forceCancel"></param>
    /// <returns></returns>
    [PublicAPI]
    public bool TryPlayAnimation(EntityUid entity,
        ProtoId<CEAnimationActionPrototype> animationProto,
        bool forceCancel = false)
    {
        if (TryComp<CEActiveAnimationActionComponent>(entity, out var controller))
        {
            if (forceCancel)
                CancelAnimation((entity, controller));
            else
                return false;
        }

        if (!_proto.Resolve(animationProto, out var indexedAnimation))
            return false;

        StartAnimation(entity, indexedAnimation);
        return true;
    }

    /// <summary>
    ///
    /// </summary>
    [PublicAPI]
    public void CancelAnimation(Entity<CEActiveAnimationActionComponent> entity)
    {
        if (!_proto.Resolve(entity.Comp.ActiveAnimation, out var animation))
            return;

        //Canceling
        var cancelEv = new CEAnimationActionEndedEvent(animation, true);
        RaiseLocalEvent(entity, cancelEv);
        StopAnimation(entity);
    }

    /// <summary>
    /// Starts the specified animation, overwriting the current animations if they are playing.
    /// </summary>
    private void StartAnimation(EntityUid entity, CEAnimationActionPrototype animation)
    {
        var controller = EnsureComp<CEActiveAnimationActionComponent>(entity);

        controller.ActiveAnimation = animation;
        controller.StartAnimationTime = _timing.CurTime;
        controller.LockRotation = animation.LockRotation;
        controller.LastEvent = TimeSpan.Zero; // Reset last event for new animation
        Dirty(entity, controller);

        var started = new CEAnimationActionStartedEvent(animation);
        RaiseLocalEvent(entity, started);

        _movement.RefreshMovementSpeedModifiers(entity);
    }

    /// <summary>
    /// Stops any animation being played on an entity
    /// </summary>
    /// <param name="entity"></param>
    private void StopAnimation(Entity<CEActiveAnimationActionComponent> entity)
    {
        RemComp<CEActiveAnimationActionComponent>(entity);
        _movement.RefreshMovementSpeedModifiers(entity);
    }
}

/// <summary>
///
/// </summary>
/// <param name="animation"></param>
/// <param name="cancelled"></param>
public sealed class CEAnimationActionEndedEvent(ProtoId<CEAnimationActionPrototype> animation, bool cancelled)
    : EntityEventArgs
{
    public ProtoId<CEAnimationActionPrototype> Animation = animation;
    public bool Cancelled = cancelled;
}

/// <summary>
///
/// </summary>
/// <param name="animation"></param>
public sealed class CEAnimationActionStartedEvent(ProtoId<CEAnimationActionPrototype> animation) : EntityEventArgs
{
    public ProtoId<CEAnimationActionPrototype> Animation = animation;
}
