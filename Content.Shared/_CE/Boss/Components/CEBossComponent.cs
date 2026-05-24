using Robust.Shared.GameStates;

namespace Content.Shared._CE.Boss.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEBossComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool StartedFight;
}
