using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.DivineShieldBreakEffect;

//Spawn EntityEffects when divine shield broken
[RegisterComponent, NetworkedComponent]
public sealed partial class CEDivineShieldBreakEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();
}
