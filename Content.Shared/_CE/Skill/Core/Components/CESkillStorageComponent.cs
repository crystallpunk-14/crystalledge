using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Core.Components;

/// <summary>
/// Component that stores the skills learned by a player and their progress in the skill trees.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(Skill.Core.CESharedSkillSystem))]
public sealed partial class CESkillStorageComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<CESkillPrototype>> LearnedSkills = new();
}
