using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.VitalityMaxHealth;

/// <summary>
/// Links the Vitality stat to mob state thresholds via <see cref="CEVitalityMaxHealthSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEVitalityMaxHealthSystem))]
public sealed partial class CEVitalityMaxHealthComponent : Component
{
    /// <summary>
    /// Critical threshold per point of vitality stat.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HealthPerVitality = 4f;
}
