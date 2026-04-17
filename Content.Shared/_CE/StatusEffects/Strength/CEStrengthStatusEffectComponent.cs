using Content.Shared._CE.Health;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.Strength;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEStrengthStatusEffectComponent : Component
{
    /// <summary>
    /// Bonus damage to add per stack when the affected entity performs a melee attack.
    /// </summary>
    [DataField]
    public CEDamageSpecifier BonusDamagePerStack = new();
}
