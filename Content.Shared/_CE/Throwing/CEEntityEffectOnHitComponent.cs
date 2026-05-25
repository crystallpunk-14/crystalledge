using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Throwable;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEEntityEffectOnHitComponent : Component
{
    [DataField(required: true)]
    public List<CEEntityEffect> HitEffects = new();
}
