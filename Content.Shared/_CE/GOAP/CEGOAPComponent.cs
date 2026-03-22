using Robust.Shared.GameStates;
using Robust.Shared.Map;
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
    /// Named targets resolved by sensors.
    /// Keys are logical target names (e.g. "enemy"), values are resolved entity UIDs.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid?> Targets = new();

    /// <summary>
    /// Last known coordinates for each target key, with an expiry time.
    /// Updated automatically when a target is set; cleared when the memory duration expires.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, MemorizedPosition> LastKnownPositions = new();

    /// <summary>
    /// How long the entity remembers a target's last known position after losing sight.
    /// </summary>
    [DataField(serverOnly: true)]
    public TimeSpan TargetMemoryDuration = TimeSpan.FromSeconds(10);

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

}

/// <summary>
/// A remembered target position with an expiry time.
/// </summary>
public record struct MemorizedPosition(EntityCoordinates Coordinates, TimeSpan ExpireAt);

