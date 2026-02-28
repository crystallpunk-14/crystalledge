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
    [Dependency] private readonly SharedTransformSystem _transform = default!;
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

            var speedMultiplier = 1f / controller.AnimationSpeed;

            var animationEndTime = controller.StartAnimationTime + (animation.Duration * speedMultiplier);

            //Finishing animation
            if (_timing.CurTime >= animationEndTime)
            {
                var finishedEv = new CEAnimationActionEndedEvent(animation, false);
                RaiseLocalEvent(uid, finishedEv);
                StopAnimation((uid, controller));
                continue;
            }

            if (_timing.ApplyingState)
                continue;

            //Processing animation events
            if (animation.Events.Any() && controller.StartAnimationTime.HasValue)
            {
                var startTime = controller.StartAnimationTime.Value;
                foreach (var (keyFrame, actions) in animation.Events)
                {
                    var realKeyFrame = keyFrame * speedMultiplier;
                    // Skip events already processed
                    if (realKeyFrame <= controller.LastEvent)
                        continue;

                    var eventTime = startTime + realKeyFrame;
                    // Only trigger if event time is within this frame
                    if (eventTime > controller.LastEvent && eventTime <= _timing.CurTime)
                    {
                        foreach (var action in actions)
                        {
                            action.Play(EntityManager, uid, controller.Used, controller.AnimationAngle ?? Angle.Zero, controller.AnimationSpeed, keyFrame);
                        }
                        controller.LastEvent = realKeyFrame;
                        Dirty(uid, controller);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Attempts to run animation on entity.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="animationProto"></param>
    /// <param name="used"></param>
    /// <param name="angle"></param>
    /// <param name="speed">The speed at which you need to start a new animation</param>
    /// <param name="forceCancel">Forcefully cancel the currently playing animation to start a new one</param>
    /// <returns></returns>
    [PublicAPI]
    public bool TryPlayAnimation(EntityUid entity,
        ProtoId<CEAnimationActionPrototype> animationProto,
        EntityUid? used = null,
        Angle? angle = null,
        float speed = 1f,
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

        StartAnimation(entity, indexedAnimation, used, angle, speed);
        return true;
    }

    /// <summary>
    /// Prematurely cancels animation execution
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
    private void StartAnimation(
        EntityUid entity,
        CEAnimationActionPrototype animation,
        EntityUid? used = null,
        Angle? animationAngle = null,
        float speed = 1f)
    {
        var controller = EnsureComp<CEActiveAnimationActionComponent>(entity);

        controller.ActiveAnimation = animation;
        controller.StartAnimationTime = _timing.CurTime;
        controller.LockRotation = animation.LockRotation;
        controller.AnimationAngle = animationAngle ?? _transform.GetWorldRotation(entity);
        controller.Used = used;
        controller.AnimationSpeed = speed;
        Dirty(entity, controller);

        // Face the animation direction before locking rotation.
        // For players this points toward the mouse; for NPCs toward the target.
        if (animation.LockRotation && animationAngle != null)
            _transform.SetWorldRotation(entity, animationAngle.Value);

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
