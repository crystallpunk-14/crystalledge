using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.GOAP.Actions;

/// <summary>
/// Navigates to the last-known position of a target and wanders around it
/// until the memorized position expires.
/// </summary>
public sealed partial class CEGOAPMoveToLastKnownPositionAction
    : CEGOAPActionBase<CEGOAPMoveToLastKnownPositionAction>
{
    /// <summary>
    /// The target key to look up in LastKnownPositions.
    /// </summary>
    [DataField(required: true)]
    public string PositionTargetKey = string.Empty;

    /// <summary>
    /// How close to get before considering arrival at a waypoint.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Radius around the last-known position to wander in.
    /// </summary>
    [DataField]
    public float SearchRadius = 6f;

    /// <summary>
    /// Number of random directions to sample when picking a wander point.
    /// </summary>
    [DataField]
    public int SampleDirections = 8;

    [DataField]
    public TimeSpan SearchTime = TimeSpan.FromSeconds(10f);

    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    public TimeSpan EndSearchTime = TimeSpan.Zero;
}
