using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// CrystallEdge GOAP NPC Component. Contains goals, available actions, and sensors
/// for goal-oriented action planning AI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEGOAPComponent : Component
{
    /// <summary>
    /// List of goals this entity can pursue.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CEGOAPGoal> Goals = new();

    /// <summary>
    /// Available actions this entity can perform.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CEGOAPAction> Actions = new();

    /// <summary>
    /// Sensors that update the world state each frame.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CEGOAPSensor> Sensors = new();

    /// <summary>
    /// Named target providers that resolve entity/coordinate targets.
    /// Resolved each sensor tick before sensors update.
    /// </summary>
    [DataField(serverOnly: true)]
    public Dictionary<string, CEGOAPTargetProvider> TargetProviders = new();

    /// <summary>
    /// Current world state as perceived by this entity.
    /// Keys are condition prototype IDs, values are boolean states.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, bool> WorldState = new();

    /// <summary>
    /// Current plan being executed. Null if no plan.
    /// </summary>
    [ViewVariables]
    public List<CEGOAPAction>? CurrentPlan;

    /// <summary>
    /// Index of the currently executing action in the plan.
    /// </summary>
    [ViewVariables]
    public int CurrentActionIndex;

    /// <summary>
    /// Whether the current action has had its startup event raised.
    /// </summary>
    [ViewVariables]
    public bool CurrentActionStarted;

    /// <summary>
    /// The currently active goal being pursued (index into Goals list, -1 if none).
    /// </summary>
    [ViewVariables]
    public int ActiveGoalIndex = -1;

    /// <summary>
    /// Time between re-planning attempts.
    /// </summary>
    [DataField]
    public TimeSpan PlanCooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// The next game time at which re-planning is allowed.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextPlanTime;

    /// <summary>
    /// The next game time at which sensors are updated.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSensorTime;

}

