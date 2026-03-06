
using Robust.Shared.GameStates;

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
    /// Current world state as perceived by this entity.
    /// Keys are condition prototype IDs, values are boolean states.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, bool> WorldState = new();

    /// <summary>
    /// The current target entity (e.g., enemy to attack or flee from).
    /// Set by sensors, used by actions.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;

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
    /// The currently active goal being pursued.
    /// </summary>
    [ViewVariables]
    public CEGOAPGoal? ActiveGoal;

    /// <summary>
    /// Time between re-planning attempts in seconds.
    /// </summary>
    [DataField]
    public float PlanCooldown = 0.5f;

    /// <summary>
    /// Accumulator for plan cooldown.
    /// </summary>
    [ViewVariables]
    public float PlanAccumulator;

    /// <summary>
    /// Whether this GOAP agent is enabled.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}

