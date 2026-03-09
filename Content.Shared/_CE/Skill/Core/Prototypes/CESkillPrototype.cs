using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Skill.Core.Prototypes;

[Prototype("skill")]
public sealed partial class CESkillPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CESkillPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

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
    [DataField(required: true)]
    public CESkillEffect Effect = default!;

    /// <summary>
    /// Skill restriction. Restrictions on this skill entering the pool of possible skills when the player levels up.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CESkillRestriction> Restrictions = new();

    /// <summary>
    /// The visual effect visible around the skill while it is in the world as a pickable enhancement.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Vfx;

    /// <summary>
    /// Light color for the skill while it is in the world as a pickable enhancement.
    /// </summary>
    [DataField]
    public Color Color = Color.White;
}

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CESkillEffect
{
    public abstract LocId SkillType { get; }
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
