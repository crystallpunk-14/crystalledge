using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEClothingHealBonusComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Flat;
}
