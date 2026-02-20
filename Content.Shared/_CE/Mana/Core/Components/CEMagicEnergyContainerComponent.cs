using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Mana.Core.Components;

/// <summary>
/// Allows an item to store magical energy within itself.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedMagicEnergySystem))]
public sealed partial class CEMagicEnergyContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Energy = 0;

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxEnergy = 100;

    /// <summary>
    /// Does this container support unsafe energy manipulation?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UnsafeSupport;
}
