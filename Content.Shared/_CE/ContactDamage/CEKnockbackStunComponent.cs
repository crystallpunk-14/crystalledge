using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.ContactDamage;

/// <summary>
/// Temporarily blocks movement after a contact-damage knockback.
/// Added and removed by <see cref="CEContactDamageSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CEKnockbackStunComponent : Component
{
    /// <summary>
    /// How long movement is blocked after a knockback hit.
    /// </summary>
    [DataField]
    public TimeSpan KnockbackDuration = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Absolute game time at which the movement block expires.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan BlockUntil = TimeSpan.Zero;
}
