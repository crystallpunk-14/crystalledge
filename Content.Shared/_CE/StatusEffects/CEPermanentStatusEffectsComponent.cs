using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEPermanentStatusEffectsComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntProtoId> Effects = new();
}
