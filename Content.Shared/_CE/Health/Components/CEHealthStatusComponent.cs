using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// When present on an entity, shows health/durability status when the item is held in hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEHealthStatusComponent : Component;
