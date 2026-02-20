using Robust.Shared.GameStates;

namespace Content.Shared._CE.Mana.Core.Components;

/// <summary>
/// Allows you to examine how much energy is in that object.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CESharedMagicEnergySystem))]
public sealed partial class CEMagicEnergyExaminableComponent : Component;
