using Content.Shared._CE.Health;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skills.DanceOfFireAndIce;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEDanceOfFireAndIceComponent : Component
{
    /// <summary>
    /// Bonus fire damage dealt to frozen targets.
    /// </summary>
    [DataField]
    public int FireBonusVsFrozen = 15;

    /// <summary>
    /// Bonus cold damage dealt to burning targets.
    /// </summary>
    [DataField]
    public int ColdBonusVsBurning = 15;

    /// <summary>
    /// Status effect prototype that indicates a target is frozen.
    /// </summary>
    [DataField]
    public EntProtoId FrozenEffect = "CEStatusEffectColdSlowdown";

    /// <summary>
    /// Status effect prototype that indicates a target is on fire.
    /// </summary>
    [DataField]
    public EntProtoId BurningEffect = "CEStatusEffectFire";
}
