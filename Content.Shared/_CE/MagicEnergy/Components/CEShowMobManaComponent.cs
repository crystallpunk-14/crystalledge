using Content.Shared.Power.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.MagicEnergy.Components;

/// <summary>
/// When attached to an entity (directly or via equipment), enables the CE mana bar overlay
/// that shows mana bars above entities with <see cref="BatteryComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEShowMobManaComponent : Component;
