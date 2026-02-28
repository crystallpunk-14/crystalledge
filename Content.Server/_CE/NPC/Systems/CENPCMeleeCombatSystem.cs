using System.Numerics;
using Content.Server._CE.Animation.Core;
using Content.Server._CE.Animation.Item;
using Content.Server._CE.NPC.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared.NPC;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.NPC.Systems;

public sealed partial class CENPCMeleeCombatSystem : EntitySystem
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEWeaponSystem _weapon = default!;
    [Dependency] private readonly CEAnimationActionSystem _animation = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float TargetMeleeLostRange = 14f;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<CENPCMeleeCombatComponent, ComponentShutdown>(OnMeleeShutdown);
    }

    private void OnMeleeShutdown(Entity<CENPCMeleeCombatComponent> ent, ref ComponentShutdown args)
    {
        _steering.Unregister(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CENPCMeleeCombatComponent, ActiveNPCComponent>();
        while (query.MoveNext(out var uid, out var combat, out _))
        {
            Attack((uid, combat));
        }
    }

    private void Attack(Entity<CENPCMeleeCombatComponent> ent)
    {
        ent.Comp.Status = CECombatStatus.Normal;

        if (!_weapon.TryGetWeapon(ent, out var weapon))
        {
            ent.Comp.Status = CECombatStatus.NoWeapon;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(ent.Comp.Target, out var targetXform))
        {
            ent.Comp.Status = CECombatStatus.TargetUnreachable;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            ent.Comp.Status = CECombatStatus.TargetUnreachable;
            return;
        }

        if (distance > TargetMeleeLostRange)
        {
            ent.Comp.Status = CECombatStatus.TargetUnreachable;
            return;
        }

        if (TryComp<NPCSteeringComponent>(ent, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            ent.Comp.Status = CECombatStatus.TargetUnreachable;
            return;
        }

        _steering.Register(ent, new EntityCoordinates(ent.Comp.Target, Vector2.Zero), steering);

        if (distance > weapon.Value.Comp.NPCAttackRange)
        {
            ent.Comp.Status = CECombatStatus.TargetOutOfRange;
            return;
        }

        var ownerPos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var direction = targetPos - ownerPos;
        var angle = direction == Vector2.Zero ? Angle.Zero : Angle.FromWorldVec(direction);

        angle += Angle.FromDegrees(_random.NextFloat(-ent.Comp.AngleVariation, ent.Comp.AngleVariation));
        _weapon.TryUse(ent, ent.Comp.UseType, angle);
    }
}
