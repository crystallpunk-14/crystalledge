using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Applies CE damage to the entity when it lands after being thrown.
/// Used for fragile items like throwable potions.
/// Requires <see cref="CEDamageableComponent"/> on the same entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDamageOnLandComponent : Component
{
    /// <summary>
    /// Amount of CE damage applied on landing.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public CEDamageSpecifier Damage;
}
