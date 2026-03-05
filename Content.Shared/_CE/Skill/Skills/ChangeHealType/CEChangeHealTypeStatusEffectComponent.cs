using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.EffectiveHeal;

/// <summary>
/// Replaces healing with damage of the specified <see cref="Target"/> damage type.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEChangeHealTypeStatusEffectComponent : Component
{
    [DataField]
    public ProtoId<CEDamageTypePrototype> Target;
}
