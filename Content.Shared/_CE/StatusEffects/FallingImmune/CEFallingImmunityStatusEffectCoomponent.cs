using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.FallingImmune;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEFallingImmunityStatusEffectComponent : Component
{
    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public float StunMultiplier = 1f;
}
