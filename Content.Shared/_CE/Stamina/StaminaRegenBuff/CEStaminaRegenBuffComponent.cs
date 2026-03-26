using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stamina.StaminaRegenBuff;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEStaminaRegenBuffComponent : Component
{
    [DataField]
    public float FlatRegenBonus = 1f;
}
