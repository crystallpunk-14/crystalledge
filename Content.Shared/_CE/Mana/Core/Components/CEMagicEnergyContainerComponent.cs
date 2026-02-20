using Robust.Shared.GameStates;

namespace Content.Shared._CE.Mana.Core.Components;

/// <summary>
/// Allows an item to store magical energy within itself.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedMagicEnergySystem))]
public sealed partial class CEMagicEnergyContainerComponent : Component
{
    /// <summary>
    /// How much energy has been spent (consumed) from this container.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SpentEnergy = 0;

    [DataField, AutoNetworkedField]
    public int MaxEnergy = 10;

    /// <summary>
    /// Current available energy, computed as <see cref="MaxEnergy"/> minus <see cref="SpentEnergy"/>.
    /// </summary>
    public int Energy => MaxEnergy - SpentEnergy;
}
