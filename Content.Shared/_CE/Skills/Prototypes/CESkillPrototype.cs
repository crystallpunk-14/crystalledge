using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Skills.Prototypes;

[Prototype("skill")]
public sealed partial class CESkillPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Skill Title. If you leave null, the name will try to generate from Effect.GetName()
    /// </summary>
    [DataField("name")]
    public LocId? NameOverride = null;

    /// <summary>
    /// Skill Description. If you leave null, the description will try to generate from Effect.GetDescription()
    /// </summary>
    [DataField("desc")]
    public LocId? DescOverride = null;

    /// <summary>
    ///  Icon for the skill. This is used to display the skill in the skill tree UI.
    /// If you leave null, the description will try to generate from Effect.GetIcon()
    /// </summary>
    [DataField("icon")]
    public SpriteSpecifier? IconOverride = default!;

    /// <summary>
    ///  Skill effect. This is used to determine what happens when the player learns the skill. If you leave null, the skill will not have any effect.
    ///  But the presence of the skill itself can affect some systems that check for the presence of certain skills.
    /// </summary>
    [DataField]
    public List<CESkillEffect> Effects = new();

    /// <summary>
    /// Skill restriction. Restrictions on this skill entering the pool of possible skills when the player levels up.
    /// </summary>
    [DataField]
    public List<CESkillRestriction> Restrictions = new();

    /// <summary>
    /// If true, the player can only have one instance of this skill. If false, the player can learn this skill multiple times.
    /// </summary>
    [DataField]
    public bool Unique = true;
}

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CESkillEffect
{
    public abstract void AddSkill(IEntityManager entManager, EntityUid target);

    public abstract void RemoveSkill(IEntityManager entManager, EntityUid target);

    public abstract string? GetName(IEntityManager entManager, IPrototypeManager protoManager);

    public abstract string? GetDescription(IEntityManager entManager, IPrototypeManager protoManager, ProtoId<CESkillPrototype> skill);

    public abstract SpriteSpecifier? GetIcon(IEntityManager entManager, IPrototypeManager protoManager);
}

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CESkillRestriction
{
    public abstract bool Check(IEntityManager entManager, EntityUid target);
}
