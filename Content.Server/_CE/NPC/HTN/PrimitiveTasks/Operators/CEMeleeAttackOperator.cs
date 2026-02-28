using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._CE.NPC.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Components;
using Content.Shared._CE.Animation.Item;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared.CombatMode;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CE.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// HTN operator that performs an animation-based melee attack
/// using <see cref="CESharedAnimationActionSystem"/> and <see cref="CESharedWeaponSystem"/>.
/// Plays one attack animation cycle (with combo cycling) and finishes when the animation completes.
/// </summary>
public sealed partial class CEMeleeAttackOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private CESharedWeaponSystem _weapon = default!;
    private CESharedAnimationActionSystem _animationAction = default!;
    private SharedCombatModeSystem _combatMode = default!;
    private SharedTransformSystem _transform = default!;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Blackboard key containing the target entity.
    /// </summary>
    [DataField( required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// The minimum damage state the target must be in for us to attack.
    /// </summary>
    [DataField]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// Which attack type to use (Primary or Secondary).
    /// </summary>
    [DataField]
    public CEUseType UseType = CEUseType.Primary;

    /// <summary>
    /// Blackboard key for the attack range.
    /// Used by <c>TargetInRangePrecondition</c> and <c>MoveToOperator</c> as well.
    /// </summary>
    [DataField]
    public string RangeKey = "MeleeRange";

    [DataField]
    public float AngleVariation = 5f;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _weapon = sysManager.GetEntitySystem<CESharedWeaponSystem>();
        _animationAction = sysManager.GetEntitySystem<CESharedAnimationActionSystem>();
        _combatMode = sysManager.GetEntitySystem<SharedCombatModeSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var comp = _entManager.EnsureComponent<CENPCMeleeCombatComponent>(owner);
        comp.Target = blackboard.GetValue<EntityUid>(TargetKey);
        comp.UseType = UseType;
        comp.AngleVariation = AngleVariation;
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        // Don't attack if they're already as wounded as we want them.
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return (false, null);

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
            return (false, null);

        return (true, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);
        HTNOperatorStatus status;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.TryGetComponent<CENPCMeleeCombatComponent>(owner, out var combat) &&
            blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) &&
            target != EntityUid.Invalid)
        {
            combat.Target = target;

            //Success
            if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
                mobState.CurrentState > TargetState)
            {
                status = HTNOperatorStatus.Finished;
            }
            else
            {
                switch (combat.Status)
                {
                    case CECombatStatus.TargetOutOfRange:
                    case CECombatStatus.Normal:
                        status = HTNOperatorStatus.Continuing;
                        break;
                    default:
                        status = HTNOperatorStatus.Failed;
                        break;
                }
            }
        }
        else
        {
            status = HTNOperatorStatus.Failed;
        }

        // Mark it as finished to continue the plan.
        if (status == HTNOperatorStatus.Continuing && ShutdownState == HTNPlanState.PlanFinished)
        {
            status = HTNOperatorStatus.Finished;
        }

        return status;
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Cancel any running animation.
        if (_entManager.TryGetComponent<CEActiveAnimationActionComponent>(owner, out var anim))
            _animationAction.CancelAnimation((owner, anim));

        // Disable combat mode.
        _combatMode.SetInCombatMode(owner, false);

        // Remove tracking component.
        _entManager.RemoveComponent<CENPCMeleeCombatComponent>(owner);

        // Clean up target from blackboard.
        blackboard.Remove<EntityUid>(TargetKey);
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        ConditionalShutdown(blackboard);
    }

    public override void PlanShutdown(NPCBlackboard blackboard)
    {
        base.PlanShutdown(blackboard);
        ConditionalShutdown(blackboard);
    }
}
