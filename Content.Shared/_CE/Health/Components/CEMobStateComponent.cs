using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores mob state thresholds and the current state.
/// When damage reaches <see cref="CriticalThreshold"/>, the entity enters Critical.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedDamageableSystem), typeof(CEMobStateSystem))]
public sealed partial class CEMobStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public CEMobState CurrentState = CEMobState.Alive;

    /// <summary>
    /// Base maximum health before modifiers.
    /// Used as the starting value for <see cref="CECalculateMaxHealthEvent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BaseMaxHealth = 20;

    /// <summary>
    /// Effective maximum health after modifiers (flat + multipliers).
    /// Damage at or above this value causes the entity to enter Critical state.
    /// Set by <see cref="CEMobStateSystem.RefreshMaxHealth"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CriticalThreshold = 20;
}

[Serializable, NetSerializable]
public enum CEMobState : byte
{
    Alive,
    Critical,
}

[Serializable, NetSerializable]
public enum CEMobStateVisuals : byte
{
    State,
}
