using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.DanceOfFireAndIce;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEDanceOfFireAndIceComponent : Component
{
    /// <summary>
    /// Bonus fire damage dealt to frozen targets.
    /// </summary>
    [DataField]
    public int FireBonusVsFrozen = 10;

    /// <summary>
    /// Bonus cold damage dealt to burning targets.
    /// </summary>
    [DataField]
    public int ColdBonusVsBurning = 10;

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
