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
    /// The dungeon level prototype that this exit leads to.
    /// Can be left empty if the owning dungeon level prototype defines exits via <c>Exits</c> dictionary;
    /// in that case, it will be assigned at runtime based on <see cref="PassageSlot"/>.
    /// </summary>
    [DataField]
    public ProtoId<CEDungeonLevelPrototype>? TargetLevel;

    /// <summary>
    /// Slot name for matching this exit to the owning prototype's <c>Exits</c> dictionary.
    /// When the dungeon is generated, exits with a matching slot get their <see cref="TargetLevel"/>
    /// assigned from the prototype.
    /// </summary>
    [DataField]
    public string PassageSlot = "default";

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
}
