using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Instance.Components;

/// <summary>
/// Marks an entity as a dungeon level exit (portal).
/// Players interact with this entity to travel to the target dungeon level.
/// The system gathers nearby players (up to <see cref="Throughput"/>), finds or creates
/// an instance of <see cref="TargetLevel"/>, and teleports the group there.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonPassageComponent : Component
{
    /// <summary>
    /// Slot number used to look up the actual target level from the owning dungeon prototype's
    /// <see cref="CEDungeonLevelPrototype.Exits"/> dictionary at activation time.
    /// Multiple exits in the same dungeon use distinct slot numbers (1, 2, 3, ...).
    /// </summary>
    [DataField]
    public int TargetLevel = 1;

    /// <summary>
    /// Time in seconds the transition takes (DoAfter duration).
    /// During this time, dungeon generation may occur in the background.
    /// </summary>
    [DataField]
    public float TransitionDuration = 10f;

    [DataField]
    public EntProtoId ActivePassageProto = "CEDungeonLevelActivePassage";

    [DataField]
    public EntityUid? ActivePassage;

    [DataField]
    public int MaxPlayers = 4;
}
