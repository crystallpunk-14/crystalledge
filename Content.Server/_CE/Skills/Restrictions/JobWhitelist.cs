using Content.Server.Roles.Jobs;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared._CE.StatusEffects;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Restrictions;

public sealed partial class JobWhitelist : CESkillRestriction
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var mindSys = entManager.System<SharedMindSystem>();
        var jobSys = entManager.System<JobSystem>();

        if (!mindSys.TryGetMind(target, out var mindId, out _))
            return false;

        if (!jobSys.MindTryGetJobId(mindId, out var jobId))
            return false;

        if (jobId is null)
            return false;

        if (Inverted)
            return !Jobs.Contains(jobId.Value);

        if (Jobs.Contains(jobId.Value))
            return true;

        if (entManager.TryGetComponent<StatusEffectContainerComponent>(target, out var container)
            && container.ActiveStatusEffects is not null)
        {
            foreach (var effect in container.ActiveStatusEffects.ContainedEntities)
            {
                if (entManager.TryGetComponent<CEAdditionalRoleSkillsStatusEffectComponent>(effect, out var roles)
                    && roles.Roles.Overlaps(Jobs))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
