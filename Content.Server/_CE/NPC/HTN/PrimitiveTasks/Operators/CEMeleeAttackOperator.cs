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
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._CE.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// HTN operator that performs an animation-based melee attack
/// using <see cref="CESharedAnimationActionSystem"/> and <see cref="CESharedItemAnimationSystem"/>.
/// Plays one attack animation cycle (with combo cycling) and finishes when the animation completes.
/// </summary>
public sealed partial class CEMeleeAttackOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private CESharedItemAnimationSystem _itemAnimation = default!;
    private CESharedAnimationActionSystem _animationAction = default!;
    private SharedCombatModeSystem _combatMode = default!;
    private SharedTransformSystem _transform = default!;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Blackboard key containing the target entity.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// The minimum damage state the target must be in for us to attack.
    /// </summary>
    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    /// <summary>
    /// Which attack type to use (Primary or Secondary).
    /// </summary>
    [DataField("useType")]
    public CEUseType UseType = CEUseType.Primary;

    /// <summary>
    /// Blackboard key for the attack range.
    /// Used by <c>TargetInRangePrecondition</c> and <c>MoveToOperator</c> as well.
    /// </summary>
    [DataField("rangeKey")]
    public string RangeKey = "MeleeRange";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _itemAnimation = sysManager.GetEntitySystem<CESharedItemAnimationSystem>();
        _animationAction = sysManager.GetEntitySystem<CESharedAnimationActionSystem>();
        _combatMode = sysManager.GetEntitySystem<SharedCombatModeSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return (false, null);

        if (_entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
            return (false, null);

        return (true, null);
    }

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var comp = _entManager.EnsureComponent<CENPCMeleeAttackComponent>(owner);
        comp.AnimationStarted = false;
        comp.Target = EntityUid.Invalid;

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return;

        comp.Target = target;

        // Enable combat mode so other systems recognize us as in-combat.
        _combatMode.SetInCombatMode(owner, true);

        // Compute direction from owner to target.
        var ownerPos = _transform.GetWorldPosition(owner);
        var targetPos = _transform.GetWorldPosition(target);
        var direction = targetPos - ownerPos;
        var angle = direction == Vector2.Zero ? Angle.Zero : new Angle(direction) + Angle.FromDegrees(90);

        // Attempt animation-based attack through the item animation system.
        comp.AnimationStarted = _itemAnimation.TryUse(owner, UseType, angle);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<CENPCMeleeAttackComponent>(owner, out var comp))
            return HTNOperatorStatus.Failed;

        // Animation never started — fail immediately.
        if (!comp.AnimationStarted)
            return HTNOperatorStatus.Failed;

        // Target died — consider attack finished.
        if (blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) &&
            _entManager.TryGetComponent<MobStateComponent>(target, out var mobState) &&
            mobState.CurrentState > TargetState)
        {
            return HTNOperatorStatus.Finished;
        }

        // Animation still playing — keep going.
        if (_entManager.HasComponent<CEActiveAnimationActionComponent>(owner))
            return HTNOperatorStatus.Continuing;

        // Animation ended — attack cycle complete.
        return HTNOperatorStatus.Finished;
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
        _entManager.RemoveComponent<CENPCMeleeAttackComponent>(owner);

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
