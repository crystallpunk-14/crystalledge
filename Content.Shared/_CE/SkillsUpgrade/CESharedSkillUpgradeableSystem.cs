using Content.Shared._CE.Skills.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.SkillsUpgrade;

public abstract partial class CESharedSkillUpgradeableSystem : EntitySystem
{
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
