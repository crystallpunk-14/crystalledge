using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Health;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.BonusDamage;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEBonusDamageStatusEffectComponent : Component
{
    /// <summary>
    /// Bonus damage to add per stack when the affected entity attacks.
    /// </summary>
    [DataField]
    public CEDamageSpecifier BonusDamagePerStack = new();

    /// <summary>
    /// Which attack types this bonus applies to.
    /// </summary>
    [DataField]
    public HashSet<CEAttackType> AttackTypes = new();
}
