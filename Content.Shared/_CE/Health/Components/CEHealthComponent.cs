using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores current and maximum health for an entity.
/// Health is a single integer value. When it reaches 0, the entity enters Critical state.
/// When it drops to <see cref="DeathThreshold"/>, the entity dies.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CESharedHealthSystem))]
public sealed partial class CEHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Health = 10;

    [DataField, AutoNetworkedField]
    public int MaxHealth = 10;

    /// <summary>
    /// Health value at or below which the entity is considered dead.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DeathThreshold = -20;
}
