using Content.Shared._CE.Skills.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Skills.Components;

/// <summary>
/// Component that stores the skills learned by a player and their progress in the skill trees.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(CESharedSkillSystem))]
public sealed partial class CESkillStorageComponent : Component
{
    /// <summary>
    /// Cached value which specific skills this character can obtain
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CESkillPrototype>> PossibleSkills = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<CESkillPrototype>> LearnedSkills = new();
}

/// <summary>
/// Raised when a player attempts to learn a skill. This is sent from the client to the server.
/// </summary>
[Serializable, NetSerializable]
public sealed class CETryLearnSkillMessage(NetEntity entity, ProtoId<CESkillPrototype> skill) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
    public readonly ProtoId<CESkillPrototype> Skill = skill;
}
