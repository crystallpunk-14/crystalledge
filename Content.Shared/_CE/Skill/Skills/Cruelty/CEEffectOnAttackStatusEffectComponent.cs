using Content.Shared._CE.EntityEffect;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Skill.Skills.Cruelty;

/// <summary>
/// Apply CEEntityEffects on melee attack targets, then remove stacks of this status effect
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnAttackStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
