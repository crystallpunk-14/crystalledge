using Content.Shared._CE.Skills.Prototypes;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.SkillsUpgrade;

public abstract partial class CESharedSkillUpgradeableSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public void EnableUpgradeAlert(Entity<CESkillUpgradeableComponent> ent)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.UpgradeAlert);
    }

    public void DisableUpgradeAlert(Entity<CESkillUpgradeableComponent> ent)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.UpgradeAlert);
    }
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

public sealed partial class CESkillUpgradeAlertEvent : BaseAlertEvent;
