using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Instance.Components;

[RegisterComponent]
public sealed partial class CEDungeonActivePassageComponent : Component
{
    [DataField]
    public EntityCoordinates? TargetPosition;

    [DataField]
    public TimeSpan TransitionInitialDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan TransitionDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan NextTransitionTime = TimeSpan.Zero;

    [DataField]
    public ProtoId<CEDungeonLevelPrototype>? TargetLevel;

    /// <summary>
    /// Radius (in tiles) to search for nearby players when forming a group.
    /// </summary>
    [DataField]
    public float SearchRadius = 1.5f;

    /// <summary>
    /// Maximum number of players that can pass through this exit at once.
    /// If more players are nearby, a random subset is chosen.
    /// </summary>
    [DataField]
    public int Throughput = 4;
}
