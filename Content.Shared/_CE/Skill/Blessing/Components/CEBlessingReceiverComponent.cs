using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Blessing.Components;

/// <summary>
/// The component allows entity to receive blessings from statues.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(CESharedBlessingSystem))]
public sealed partial class CEBlessingReceiverComponent : Component
{
    /// <summary>
    /// All skills that have been proposed to the player on any pedestal.
    /// New statues avoid generating these to prevent cross-statue duplicates.
    /// Cleared when the pool is exhausted and must restart.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<CESkillPrototype>> ProposedSkills = new();

    /// <summary>
    /// True - Active, False - passive. The order of the items in this list will determine the order in which
    /// active and passive skills are offered to the player (based on the number of skills the player has).
    /// Once the list is exhausted, any available skills will be offered.
    /// </summary>
    public List<bool> SkillTypeOrder = new()
    {
        true,
        false,
        true,
        false,
        true,
        false,
    };
}
