using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.EffectiveHeal;

/// <summary>
/// Increases all outgoing healing from the player by X
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeHealTypeStatusEffectComponent : Component
{
    [DataField]
    public ProtoId<CEDamageTypePrototype> Target;
}
