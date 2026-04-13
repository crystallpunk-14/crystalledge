using Robust.Shared.GameStates;

namespace Content.Shared._CE.Frost;

/// <summary>
/// When present on a status effect entity, grants the target immunity to cold slowdown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEFreezeImmunityStatusEffectComponent : Component
{
}
