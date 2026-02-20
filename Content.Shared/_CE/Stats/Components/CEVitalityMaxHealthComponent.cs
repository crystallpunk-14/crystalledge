using Robust.Shared.GameStates;

namespace Content.Shared._CE.Stats.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CEStatsSystem))]
public sealed partial class CEVitalityMaxHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float HealthPerVitality = 4f;
}
