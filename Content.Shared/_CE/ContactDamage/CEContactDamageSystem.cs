

using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
using Content.Shared._CE.Procedural.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CE.ContactDamage;

public sealed class CEContactDamageSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damage = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEContactDamageComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CEKnockbackStunComponent, UpdateCanMoveEvent>(OnKnockbackCanMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CEKnockbackStunComponent>();
        while (query.MoveNext(out var uid, out var stun))
        {
            if (now < stun.BlockUntil)
                continue;

            RemComp<CEKnockbackStunComponent>(uid);
            _blocker.UpdateCanMove(uid);
        }
    }

    private void OnKnockbackCanMove(Entity<CEKnockbackStunComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (_timing.CurTime < ent.Comp.BlockUntil)
            args.Cancel();
    }

    private void OnStartCollide(Entity<CEContactDamageComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<CEDungeonPlayerComponent>(args.OtherEntity, out var playerComp))
            return;

        // No contact damage while the mob is asleep.
        if (HasComp<CEGOAPSleepingComponent>(args.OurEntity))
            return;

        if (_timing.CurTime < ent.Comp.NextDamageTime)
            return;

        ent.Comp.NextDamageTime = _timing.CurTime + ent.Comp.ContactDamageInterval;
        Dirty(ent);

        _damage.TakeDamage(args.OtherEntity, ent.Comp.Damage, args.OurEntity);

        if (ent.Comp.PushForce > 0f)
        {
            var enemyPos = _transform.GetWorldPosition(args.OurEntity);
            var playerPos = _transform.GetWorldPosition(args.OtherEntity);
            var direction = playerPos - enemyPos;
            if (direction != Vector2.Zero)
            {
                // Use direct velocity set instead of TryThrow to avoid ThrownItemComponent
                // which conflicts with KinematicController player bodies.
                var normalized = direction.Normalized();
                _physics.SetLinearVelocity(args.OtherEntity, normalized * ent.Comp.PushForce);

                var stun = EnsureComp<CEKnockbackStunComponent>(args.OtherEntity);
                stun.BlockUntil = _timing.CurTime + stun.KnockbackDuration;
                Dirty(args.OtherEntity, stun);
                _blocker.UpdateCanMove(args.OtherEntity);
            }
        }
    }
}
