using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Equipment;

/// <summary>
/// When this component is on a clothing item, equipping the item applies
/// the specified status effect (as 1 stack) to the wearer. Unequipping removes it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEEquipStatusEffectComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId StatusEffect = default!;
}
