using Robust.Shared.GameStates;

namespace Content.Shared._CE.Mana.Core.Components;

/// <summary>
/// Allows an item to store magical energy within itself.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedMagicEnergySystem))]
public sealed partial class CEMagicEnergyContainerComponent : Component
{
    /// <summary>
    /// Current available energy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Energy = 20;

    [DataField, AutoNetworkedField]
    public int MaxEnergy = 20;
}
