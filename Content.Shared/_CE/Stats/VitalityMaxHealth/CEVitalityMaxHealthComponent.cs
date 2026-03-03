using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.VitalityMaxHealth;

/// <summary>
/// Links the Vitality stat to max health via <see cref="CEVitalityMaxHealthSystem"/>.
/// Determines how much health an entity gains per point of vitality.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEVitalityMaxHealthSystem))]
public sealed partial class CEVitalityMaxHealthComponent : Component
{
    /// <summary>
    /// The amount of health gained per point of vitality stat.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HealthPerVitality = 4f;
}
