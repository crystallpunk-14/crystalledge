using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Skills.ReactOnDamage;

/// <summary>
/// When taking damage performs an action on the source of the attack.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEReactOnDamageStatusEffectComponent : Component
{
    [DataField]
    public ReactionType Reaction = ReactionType.Damage;

    [DataField]
    public TargetType Target = TargetType.Source;

    /// <remarks>
    /// Not used when <see cref="Reaction"/> is <see cref="ReactionType.Heal"/>.
    /// </remarks>
    [DataField]
    public ProtoId<CEDamageTypePrototype> DamageType = "Physical";

    [DataField]
    public int Amount = 0;
}

public enum ReactionType
{
    Damage,
    Heal,
}
public enum TargetType
{
    Self,
    Source
}
