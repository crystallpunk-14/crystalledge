using Robust.Shared.GameStates;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
///  Automatically marks the collectible as collected for all players who have reached the soul collection limit on this floor
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CESoulCollectableComponent : Component
{
}
