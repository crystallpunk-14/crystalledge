using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Marker component that enables floating damage/heal number popups above an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEDamagePopupComponent : Component;
