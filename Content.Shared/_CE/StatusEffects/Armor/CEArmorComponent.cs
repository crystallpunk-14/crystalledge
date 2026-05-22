using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CEArmorSystem))]
public sealed partial class CEArmorComponent : Component
{
    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<ProtoId<CEDamageTypePrototype>, int> Flat = new();

    [DataField, AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<ProtoId<CEDamageTypePrototype>, float> Multiplier = new();
}
