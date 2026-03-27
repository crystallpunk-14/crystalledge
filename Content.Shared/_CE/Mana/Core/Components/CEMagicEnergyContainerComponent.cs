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

    /// <summary>
    /// Base maximum energy before modifiers.
    /// Used as the starting value for <see cref="CECalculateMaxManaEvent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BaseMaxEnergy = 20;

    /// <summary>
    /// Effective maximum energy after modifiers (flat + multipliers).
    /// Set by <see cref="CESharedMagicEnergySystem.RefreshMaxMana"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxEnergy = 20;
}
