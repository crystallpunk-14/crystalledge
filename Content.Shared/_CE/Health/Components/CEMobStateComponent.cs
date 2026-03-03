using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Tracks the mob state (Alive, Critical, Dead) driven by <see cref="CEHealthComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedMobStateSystem))]
public sealed partial class CEMobStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public CEMobState CurrentState = CEMobState.Alive;
}

[Serializable, NetSerializable]
public enum CEMobState : byte
{
    Alive,
    Critical,
    Dead,
}
