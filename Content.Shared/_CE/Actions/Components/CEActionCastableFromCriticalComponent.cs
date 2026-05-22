using Robust.Shared.GameStates;

namespace Content.Shared._CE.Actions.Components;

/// <summary>
/// Actions without this component are blocked in critical state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEActionCastableFromCriticalComponent : Component
{
}
