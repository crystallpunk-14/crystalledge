using Robust.Shared.GameStates;

namespace Content.Shared._CE.Water;

/// <summary>
/// When attached to an item, lowers its DrawDepth below water (Puddles).
/// If the item is placed on a <see cref="Content.Shared.Placeable.PlaceableSurfaceComponent"/>,
/// its DrawDepth is raised to be above the surface entity.
/// Stores the original DrawDepth to restore on removal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEPlaceableDrawDepthComponent : Component
{
    /// <summary>
    /// DrawDepth to use when the item is NOT on a placeable surface (e.g. on the floor / in water).
    /// Defaults to FloorObjects (below Puddles).
    /// </summary>
    [DataField]
    public int LoweredDrawDepth = (int) DrawDepth.DrawDepth.FloorObjects;

    /// <summary>
    /// The original DrawDepth before this component modified it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int OriginalDrawDepth;

    /// <summary>
    /// Whether the original draw depth has been captured.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DepthInitialized;
}
