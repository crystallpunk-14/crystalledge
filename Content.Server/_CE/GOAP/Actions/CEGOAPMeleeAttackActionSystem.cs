using System.Numerics;
using Content.Server._CE.Animation.Item;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
using Content.Shared.CombatMode;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Performs a melee attack on the current target.
/// Uses absolute coordinates for steering to ensure proper pathfinding.
/// </summary>
public sealed partial class CEGOAPMeleeAttackAction : CEGOAPActionBase<CEGOAPMeleeAttackAction>
{
    [DataField]
    public CEUseType UseType = CEUseType.Primary;

    /// <summary>
    /// Random angle spread for attacks in degrees.
    /// </summary>
    [DataField]
    public float AngleVariation = 15f;

    /// <summary>
    /// Minimal distance to the target to perform the attack.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// How far the target must move before re-registering steering.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1.5f;
}

public sealed partial class CEGOAPMeleeAttackActionSystem : CEGOAPActionSystem<CEGOAPMeleeAttackAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly CEWeaponSystem _weapon = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
    }

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMeleeAttackAction> args)
    {
        _combatMode.SetInCombatMode(ent, true);

        var target = GetTarget(ent, args.Action.TargetKey);
        if (target == null || !_xformQuery.TryGetComponent(target.Value, out var targetXform))
            return;

        // Use absolute coordinates for proper pathfinding
        var comp = _steering.Register(ent, targetXform.Coordinates);
        comp.Range = args.Action.Range;
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMeleeAttackAction> args)
    {
        var target = GetTarget(ent, args.Action.TargetKey);
        if (target == null)
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Check if target is neutralized
        if (!_mobState.IsAlive(target.Value))
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
            !_xformQuery.TryGetComponent(target.Value, out var targetXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        // Re-register steering if target has moved significantly
        if (_steeringQuery.TryComp(ent, out var steeringComp))
        {
            if (steeringComp.Coordinates.TryDistance(
                    EntityManager,
                    targetXform.Coordinates,
                    out var delta)
                && delta > args.Action.ReregisterThreshold)
            {
                var comp = _steering.Register(ent, targetXform.Coordinates);
                comp.Range = args.Action.Range;
            }

            if (steeringComp.Status == SteeringStatus.NoPath)
            {
                args.Status = CEGOAPActionStatus.Failed;
                return;
            }
        }

        if (distance <= args.Action.Range)
        {
            // In range: attack
            var ownerPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(targetXform);
            var direction = targetPos - ownerPos;
            var angle = direction == Vector2.Zero
                ? Angle.Zero
                : Angle.FromWorldVec(direction);
            angle += Angle.FromDegrees(
                _random.NextFloat(-args.Action.AngleVariation, args.Action.AngleVariation));

            _weapon.TryUse(ent, weapon.Value, args.Action.UseType, angle);
        }

        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPMeleeAttackAction> args)
    {
        _combatMode.SetInCombatMode(ent, false);
        _steering.Unregister(ent);
    }
}
