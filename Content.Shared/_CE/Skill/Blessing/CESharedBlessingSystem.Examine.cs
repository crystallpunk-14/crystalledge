using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared.Examine;

namespace Content.Shared._CE.Skill.Blessing;

public abstract partial class CESharedBlessingSystem
{
    private void InitializeExamine()
    {
        SubscribeLocalEvent<CEBlessingComponent, ExaminedEvent>(OnBlessingExamined);
    }

    private void OnBlessingExamined(Entity<CEBlessingComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Skill is not { } skillId)
            return;

        var desc = _skill.GetSkillDescription(skillId);
        if (!string.IsNullOrWhiteSpace(desc))
            args.PushMarkup(desc);

        args.PushMarkup(_skill.GetSkillType(skillId));

        if (ent.Comp.ForPlayer is { } forPlayer && forPlayer != args.Examiner)
            args.PushMarkup(Loc.GetString("ce-blessing-wrong-player"));
    }
}
