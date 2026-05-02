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

    [DataField]
    public bool BlockThrow;

    [DataField]
    public bool BlockDrop;

    [DataField]
    public bool BlockPickup;

    [DataField]
    public bool BlockPull;

    [DataField]
    public bool BlockStand;

    [DataField]
    public bool BlockEquip;

    [DataField]
    public bool BlockUnequip;
}
