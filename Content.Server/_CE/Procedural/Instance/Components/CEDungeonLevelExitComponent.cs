using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Instance.Components;

/// <summary>
/// Marks an entity as a dungeon level exit (portal).
/// Players interact with this entity to travel to the target dungeon level.
/// The system gathers nearby players (up to <see cref="Throughput"/>), finds or creates
/// an instance of <see cref="TargetLevel"/>, and teleports the group there.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonLevelExitComponent : Component
{
    /// <summary>
    /// The dungeon level prototype that this exit leads to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEDungeonLevelPrototype> TargetLevel;

    /// <summary>
    /// Maximum number of players that can pass through this exit at once.
    /// If more players are nearby, a random subset is chosen.
    /// </summary>
    [DataField]
    public int Throughput = 4;

    /// <summary>
    /// Radius (in tiles) to search for nearby players when forming a group.
    /// </summary>
    [DataField]
    public float SearchRadius = 3f;

    /// <summary>
    /// Time in seconds the transition takes (DoAfter duration).
    /// During this time, dungeon generation may occur in the background.
    /// </summary>
    [DataField]
    public float TransitionDuration = 10f;
}
