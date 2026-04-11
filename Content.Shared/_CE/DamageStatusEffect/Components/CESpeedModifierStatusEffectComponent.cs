using Content.Shared._CE.Health;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.DamageStatusEffect.Components;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CESpeedModifierStatusEffectComponent : Component
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public float Speed = 1;
}
