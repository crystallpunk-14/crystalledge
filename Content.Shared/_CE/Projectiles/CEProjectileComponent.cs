using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Projectiles;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEProjectileComponent : Component
{
    [DataField(required: true)]
    public List<CEEntityEffect> HitEffects = new();
}
