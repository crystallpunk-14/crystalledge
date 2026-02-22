using Content.Shared._CE.Skills;
using Content.Shared._CE.Skills.Components;

namespace Content.Server._CE.Skills;

public sealed partial class CESkillSystem : CESharedSkillSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CETryLearnSkillMessage>(OnClientRequestLearnSkill);
    }

    private void OnClientRequestLearnSkill(CETryLearnSkillMessage ev, EntitySessionEventArgs args)
    {
        var entity = GetEntity(ev.Entity);

        if (args.SenderSession.AttachedEntity != entity)
            return;

        TryAddSkill(entity, ev.Skill);
    }
}
