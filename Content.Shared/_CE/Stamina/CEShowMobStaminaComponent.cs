using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stamina;

/// <summary>
/// When attached to an entity (directly or via equipment), enables the CE stamina bar overlay
/// that shows stamina bars above entities with <see cref="CEStaminaComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEShowMobStaminaComponent : Component;
