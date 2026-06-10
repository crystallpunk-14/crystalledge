using Robust.Shared.GameStates;

namespace Content.Shared._CE.Currency.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEStaticPriceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Price;
}
