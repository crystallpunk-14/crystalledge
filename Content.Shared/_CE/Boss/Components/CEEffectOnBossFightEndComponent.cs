using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Boss.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnBossFightEndComponent : Component
{
    [DataField(required: true)]
    public List<CEEntityEffect> Effects = new();
}
