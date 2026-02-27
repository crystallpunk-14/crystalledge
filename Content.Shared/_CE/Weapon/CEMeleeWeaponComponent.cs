using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Weapon;

/// <summary>
///
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEMeleeWeaponComponent : Component
{
    /// <summary>
    /// It may be confusing, but weapons do not deal damage “directly.”
    /// Damage from weapons is only dealt through usage animations, through the hitboxes of attacks in the animation.
    /// At the same time, different animations for the same weapon may want to deal different types of damage
    /// (piercing or slashing attacks within animations), so weapon damage is a dictionary of keys and damage types.
    /// Inside the WeaponArcAttack animation, there is a reference to damageGroup - this means that the animation
    /// uses the specified damage type.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<string, DamageSpecifier> DamageGroups = new();

    /// <summary>
    /// Modify weapon attack animations range
    /// </summary>
    [DataField]
    public float RangeMultiplier = 1f;
}
