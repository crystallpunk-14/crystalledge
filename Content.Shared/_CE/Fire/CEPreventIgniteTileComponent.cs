using Robust.Shared.GameStates;

namespace Content.Shared._CE.Fire;

/// <summary>
/// Marker component that prevents fire tile placement on this tile
/// and prevents ignition of entities standing on tiles with this component.
/// Handled by <see cref="CEFireSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEPreventIgniteTileComponent : Component;
