using Robust.Shared.GameStates;

namespace Content.Shared._CE.Actions.Components;

/// <summary>
/// Marker component that allows this action to be used while the performer is in <see cref="CEMobState.Critical"/>.
/// Actions without this component are blocked in critical state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEActionCastableFromCriticalComponent : Component
{
}
