using System.Numerics;
using Content.Server._CE.Animation.Item;
using Content.Server.NPC.Systems;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared.CombatMode;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// GOAP action that performs a melee attack on the current target.
/// </summary>
public sealed partial class CEGOAPMeleeAttackAction : CEGOAPActionBase<CEGOAPMeleeAttackAction>
{
    /// <summary>
    /// Which attack type to use (Primary or Secondary).
    /// </summary>
    [DataField]
    public CEUseType UseType = CEUseType.Primary;

    /// <summary>
    /// Random angle spread for attacks in degrees.
    /// </summary>
    [DataField]
    public float AngleVariation = 5f;
}

/// <summary>
/// Handles CEGOAPMeleeAttackAction execution.
/// Steers to target and attacks with the equipped CE weapon.
/// </summary>
public sealed partial class CEGOAPMeleeAttackActionSystem : CEGOAPActionSystem<CEGOAPMeleeAttackAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly CEWeaponSystem _weapon = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CEHealthComponent> _healthQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _healthQuery = GetEntityQuery<CEHealthComponent>();
    }

    protected override void OnActionStartup(Entity<CEGOAPComponent> ent, ref CEGOAPActionStartupEvent<CEGOAPMeleeAttackAction> args)
    {
        _combatMode.SetInCombatMode(ent, true);
    }

    protected override void OnActionUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPActionUpdateEvent<CEGOAPMeleeAttackAction> args)
    {
        if (ent.Comp.Target is not { } target)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Check if target is neutralized
        if (IsTargetNeutralized(target))
        {
            args.Status = CEGOAPActionStatus.Finished;
            return;
        }

        if (!_weapon.TryGetWeapon(ent, out var weapon))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!_xformQuery.TryGetComponent(ent, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Keep steering towards target during combat
        _steering.Register(ent, new EntityCoordinates(target, Vector2.Zero));

        if (distance > weapon.Value.Comp.NPCAttackRange)
        {
            // Out of range, keep moving
            args.Status = CEGOAPActionStatus.Running;
            return;
        }

        // In range: attack
        var ownerPos = _transform.GetWorldPosition(xform);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var direction = targetPos - ownerPos;
        var angle = direction == Vector2.Zero ? Angle.Zero : Angle.FromWorldVec(direction);
        angle += Angle.FromDegrees(_random.NextFloat(-args.Action.AngleVariation, args.Action.AngleVariation));

        _weapon.TryUse(ent, args.Action.UseType, angle);
        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(Entity<CEGOAPComponent> ent, ref CEGOAPActionShutdownEvent<CEGOAPMeleeAttackAction> args)
    {
        _combatMode.SetInCombatMode(ent, false);
        _steering.Unregister(ent);
    }

    private bool IsTargetNeutralized(EntityUid target)
    {
        if (_healthQuery.TryComp(target, out var ceHealth))
            return ceHealth.CurrentState >= CEMobState.Critical;

        return true;
    }
}
