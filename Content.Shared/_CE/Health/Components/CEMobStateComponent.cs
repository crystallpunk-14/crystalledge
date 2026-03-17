using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores mob state thresholds and the current state.
/// When damage reaches <see cref="CriticalThreshold"/>, the entity enters Critical.
/// When damage reaches <see cref="DeadThreshold"/>, the entity dies.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedDamageableSystem), typeof(CEMobStateSystem))]
public sealed partial class CEMobStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public CEMobState CurrentState = CEMobState.Alive;

    /// <summary>
    /// Damage at or above which the entity enters Critical state.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CriticalThreshold = 10;

    /// <summary>
    /// Damage at or above which the entity is Dead.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DeadThreshold = 20;
}

[Serializable, NetSerializable]
public enum CEMobState : byte
{
    Alive,
    Critical,
    Dead,
}

[Serializable, NetSerializable]
public enum CEMobStateVisuals : byte
{
    State,
}
