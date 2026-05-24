using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.IncomingHealBonus;

/// <summary>
/// Status effect that increases incoming healing on the entity it's applied to.
/// Scales with stacks via <see cref="CEStatusEffectStackComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEIncomingHealBonusStatusEffectComponent : Component
{
    [DataField]
    public int BonusPerStack = 1;
}
