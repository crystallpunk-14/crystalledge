using Robust.Shared.GameStates;

namespace Content.Shared._CE.GOAPAlarm;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEAlarmOnSpawnComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 10f;
}
