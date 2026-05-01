using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects.ActionBlocker;


[RegisterComponent, NetworkedComponent]
public sealed partial class CEActionBlockerStatusEffectComponent : Component
{
    [DataField]
    public bool BlockActions;

    [DataField]
    public bool BlockUse;

    [DataField]
    public bool BlockAttack;

    [DataField]
    public bool BlockMove;
}
