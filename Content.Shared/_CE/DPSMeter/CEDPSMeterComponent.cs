using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.DPSMeter;

/// <summary>
/// Add to an entity to track incoming damage-per-second and total damage.
/// The shared system accumulates data from <see cref="Content.Shared._CE.Health.CEDamageChangedEvent"/>;
/// the client overlay reads the networked fields and computes a live decreasing DPS display.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDPSMeterComponent : Component
{
    /// <summary>
    /// Maximum DPS reached during the current tracking session.
    /// Top line of the overlay: "Max: X.X".
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxDPS;

    /// <summary>
    /// Total accumulated damage since tracking started.
    /// Used by the client overlay to compute the live DPS: TotalDamage / elapsed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalDamage;

    /// <summary>
    /// Game time when the current tracking session started.
    /// Networked so the client can compute elapsed time independently.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StartTrackTime = TimeSpan.Zero;

    /// <summary>
    /// Game time of the last received hit.
    /// The client uses this to drive the fade-out: after <see cref="TrackTimeAfterHit"/> of
    /// silence the overlay fades over <see cref="FadeDuration"/> then the session resets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastHitTime = TimeSpan.Zero;

    /// <summary>
    /// Screen-space offset in tile units. Y+ is screen-up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset = new Vector2(0f, 0.6f);

    /// <summary>
    /// How long after the last hit before the overlay starts fading.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TrackTimeAfterHit = TimeSpan.FromSeconds(5f);

    /// <summary>
    /// Duration of the fade-out animation. After this the session resets.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan FadeDuration = TimeSpan.FromSeconds(2f);
}
