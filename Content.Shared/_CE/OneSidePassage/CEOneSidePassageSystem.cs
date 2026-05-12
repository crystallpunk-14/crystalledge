using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._CE.OneSidePassage;

/// <summary>
/// Handles the one-way passage logic for <see cref="CEOneSidePassageComponent"/>.
/// Entities approaching from the "allowed" direction (within ±90° of the entity's forward vector)
/// pass through freely; those approaching from behind are always blocked.
/// </summary>
public sealed partial class CEOneSidePassageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEOneSidePassageComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CEOneSidePassageComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CEOneSidePassageComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnPreventCollide(Entity<CEOneSidePassageComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        // Already granted passage this contact session
        if (ent.Comp.CollideExceptions.Contains(args.OtherEntity))
        {
            args.Cancelled = true;
            return;
        }

        // Chain-pull: if the puller was already granted passage, allow the pulled entity too
        if (_pulling.GetPuller(args.OtherEntity) is { } puller && ent.Comp.CollideExceptions.Contains(puller))
        {
            ent.Comp.CollideExceptions.Add(args.OtherEntity);
            Dirty(ent);
            args.Cancelled = true;
            return;
        }

        if (CanPassDirection(ent, args.OtherEntity))
        {
            // Approaching from the allowed side — let through
            ent.Comp.CollideExceptions.Add(args.OtherEntity);
            if (_pulling.GetPulling(args.OtherEntity) is { } uid)
                ent.Comp.CollideExceptions.Add(uid);

            args.Cancelled = true;
            Dirty(ent);
        }
        else
        {
            // Approaching from the blocked side — rate-limited popup
            if (_timing.CurTime >= ent.Comp.NextBlockTime)
            {
                _popup.PopupClient(Loc.GetString("ce-one-side-passage-blocked"), ent, args.OtherEntity);
                ent.Comp.NextBlockTime = _timing.CurTime + TimeSpan.FromSeconds(0.5);
                Dirty(ent);
            }
        }
    }

    private void OnStartCollide(Entity<CEOneSidePassageComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.CollideExceptions.Contains(args.OtherEntity))
        {
            // Blocked — play block sound if approaching from wrong side
            if (!CanPassDirection(ent, args.OtherEntity))
                _audio.PlayPredicted(ent.Comp.BlockSound, ent, args.OtherEntity);
            return;
        }

        // Passed through — play pass sound
        _audio.PlayPredicted(ent.Comp.PassSound, ent, args.OtherEntity);
    }

    private void OnEndCollide(Entity<CEOneSidePassageComponent> ent, ref EndCollideEvent args)
    {
        if (!args.OurFixture.Hard)
        {
            ent.Comp.CollideExceptions.Remove(args.OtherEntity);
            Dirty(ent);
        }
    }

    /// <summary>
    /// Returns true if <paramref name="other"/> is approaching from the allowed (forward-facing) side.
    /// Uses a 180° cone: the entity's forward vector defines the "pass-through" direction.
    /// Entities standing on the forward side can walk through; those on the back side cannot.
    /// </summary>
    private bool CanPassDirection(Entity<CEOneSidePassageComponent> ent, EntityUid other)
    {
        var xform = Transform(ent);
        var otherXform = Transform(other);

        var (pos, rot) = _transform.GetWorldPositionRotation(xform);
        var otherPos = _transform.GetWorldPosition(otherXform);

        // Vector from the fog to the approaching entity
        var approachAngle = (pos - otherPos).ToAngle();
        // The fog gate's facing direction (its "front")
        var rotateAngle = rot.ToWorldVec().ToAngle();

        var diff = Math.Abs(approachAngle - rotateAngle);
        diff %= MathHelper.TwoPi;
        if (diff > Math.PI)
            diff = MathHelper.TwoPi - diff;

        // Within 90° of facing direction = allowed side
        return diff < Math.PI / 2;
    }

}
