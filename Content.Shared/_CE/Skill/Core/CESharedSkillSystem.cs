using System.Text;
using Content.Shared._CE.Skill.Core.Components;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared.Administration.Managers;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Skill.Core;

public abstract partial class CESharedSkillSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeAdmin();
        InitializeScanning();
    }

    /// <summary>
    /// Directly adds the skill to the player, bypassing any checks.
    /// </summary>
    public bool TryAddSkill(EntityUid target,
        ProtoId<CESkillPrototype> skill,
        CESkillStorageComponent? component = null,
        bool free = false)
    {
        if (!Resolve(target, ref component, false))
            return false;

        if (!_proto.Resolve(skill, out var indexedSkill))
            return false;

        indexedSkill.Effect.AddSkill(EntityManager, target);

        component.LearnedSkills.Add(skill);
        Dirty(target, component);

        var learnEv = new CESkillLearnedEvent(skill, target);
        RaiseLocalEvent(target, ref learnEv);

        return true;
    }

    /// <summary>
    ///  Removes the skill from the player, bypassing any checks.
    /// </summary>
    public bool TryRemoveSkill(EntityUid target,
        ProtoId<CESkillPrototype> skill,
        CESkillStorageComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false;

        if (!component.LearnedSkills.Remove(skill))
            return false;

        if (!_proto.Resolve(skill, out var indexedSkill))
            return false;

        indexedSkill.Effect.RemoveSkill(EntityManager, target);

        Dirty(target, component);
        return true;
    }

    /// <summary>
    ///  Checks if the player has the skill.
    /// </summary>
    public bool HaveSkill(EntityUid target,
        ProtoId<CESkillPrototype> skill,
        CESkillStorageComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false;

        return component.LearnedSkills.Contains(skill);
    }

    /// <summary>
    ///  Helper function to get the skill name for a given skill prototype.
    /// </summary>
    public string GetSkillName(ProtoId<CESkillPrototype> skill)
    {
        if (!_proto.Resolve(skill, out var indexedSkill))
            return string.Empty;

        if (indexedSkill.NameOverride is not null)
            return Loc.GetString(indexedSkill.NameOverride);

        var name = indexedSkill.Effect.GetName(EntityManager, _proto);
        if (name != null)
            return name;

        return string.Empty;
    }

    /// <summary>
    ///  Helper function to get the skill description for a given skill prototype.
    /// </summary>
    public string GetSkillDescription(ProtoId<CESkillPrototype> skill)
    {
        if (!_proto.Resolve(skill, out var indexedSkill))
            return string.Empty;

        var sb = new StringBuilder();

        if (indexedSkill.DescOverride is not null)
            sb.Append(Loc.GetString(indexedSkill.DescOverride));

        sb.Append(indexedSkill.Effect.GetDescription(EntityManager, _proto, skill) + "\n");

        return sb.ToString();
    }

    public SpriteSpecifier? GetSkillIcon(ProtoId<CESkillPrototype> skill)
    {
        if (!_proto.Resolve(skill, out var indexedSkill))
            return null;

        if (indexedSkill.IconOverride is not null)
            return indexedSkill.IconOverride;

        var icon = indexedSkill.Effect.GetIcon(EntityManager, _proto);
        if (icon != null)
            return icon;

        return null;
    }

    /// <summary>
    ///  Helper function to get the skill type for a given skill prototype.
    /// </summary>
    public string GetSkillType(ProtoId<CESkillPrototype> skill)
    {
        if (!_proto.Resolve(skill, out var indexedSkill))
            return string.Empty;

        return Loc.GetString(indexedSkill.Effect.SkillType);
    }

    public bool TryResetSkills(Entity<CESkillStorageComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        for (var i = ent.Comp.LearnedSkills.Count - 1; i >= 0; i--)
        {
            TryRemoveSkill(ent, ent.Comp.LearnedSkills[i], ent.Comp);
        }

        return true;
    }
}

[ByRefEvent]
public record struct CESkillLearnedEvent(ProtoId<CESkillPrototype> Skill, EntityUid User);
