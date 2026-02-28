using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Mobs;

namespace Content.Server._CE.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Attacks the specified key in melee combat.
/// </summary>
public sealed partial class CEMeleeAttackOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Key that contains the target entity.
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = default!;

    /// <summary>
    /// Minimum damage state that the target has to be in for us to consider attacking.
    /// </summary>
    [DataField("targetState")]
    public MobState TargetState = MobState.Alive;

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        throw new NotImplementedException();
    }
}
