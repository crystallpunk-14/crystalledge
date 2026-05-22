using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// When placed on a status effect entity, grants the affected character
/// access to skills of additional job roles during blessing selection.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEAdditionalRoleSkillsStatusEffectComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Roles = new();
}
