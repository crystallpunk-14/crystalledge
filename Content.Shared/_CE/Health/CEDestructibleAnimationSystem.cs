using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Handles death animations for entities with <see cref="CEDestructibleAnimationComponent"/>.
/// Intercepts <see cref="CEDestructAttemptEvent"/>, plays the configured animation,
/// and only allows destruction once the animation completes.
/// </summary>
public sealed partial class CEDestructibleAnimationSystem : EntitySystem
{
    [Dependency] private CESharedAnimationActionSystem _animation = default!;
    [Dependency] private CEDestructibleSystem _destructible = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDestructibleAnimationComponent, CEDestructAttemptEvent>(OnDestructAttempt);
        SubscribeLocalEvent<CEGOAPComponent, CEDestructAttemptEvent>(GOAPDestruct);
        SubscribeLocalEvent<CEDestructibleAnimationComponent, CEAnimationActionEndedEvent>(OnAnimationEnded);
    }

    private void GOAPDestruct(Entity<CEGOAPComponent> ent, ref CEDestructAttemptEvent args)
    {
        RemCompDeferred<CEGOAPComponent>(ent);
    }

    private void OnDestructAttempt(Entity<CEDestructibleAnimationComponent> ent, ref CEDestructAttemptEvent args)
    {
        // Death animation already finished — let destruction proceed.
        if (ent.Comp.PendingDestruction)
            return;

        // Cancel the destruction attempt.
        args.Cancelled = true;

        // Don't restart the animation if it is already playing from an earlier lethal hit.
        if (ent.Comp.IsPlayingDeathAnimation)
            return;

        ent.Comp.PendingSource = args.Source;
        ent.Comp.IsPlayingDeathAnimation = true;

        _animation.TryPlayAnimationToAngle(ent, ent.Comp.Animation, forceCancel: true);
    }

    private void OnAnimationEnded(Entity<CEDestructibleAnimationComponent> ent, ref CEAnimationActionEndedEvent args)
    {
        if (!ent.Comp.IsPlayingDeathAnimation)
            return;

        // Only react to our own death animation, not to other animations finishing.
        if (args.Animation != ent.Comp.Animation)
            return;

        // Destruction is server-authoritative; mirror the guard used in CEDestructibleSystem.
        if (!_timing.IsFirstTimePredicted)
            return;

        ent.Comp.IsPlayingDeathAnimation = false;
        ent.Comp.PendingDestruction = true;

        _destructible.ForceDestruct(ent, ent.Comp.PendingSource);
    }
}
