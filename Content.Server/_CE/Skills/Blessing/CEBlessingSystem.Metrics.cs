using Content.Server.Roles.Jobs;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared.Mind;
using Prometheus;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Blessing;

public sealed partial class CEBlessingSystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly JobSystem _job = default!;

    private static readonly Counter SkillsOffered = Prometheus.Metrics.CreateCounter(
        "crystall_edge_blessing_skill_offered_total",
        "Total times a skill was offered to a player at a blessing statue.",
        "skill",
        "job",
        "skill_type");

    private static readonly Counter SkillsChosen = Prometheus.Metrics.CreateCounter(
        "crystall_edge_blessing_skill_chosen_total",
        "Total times a player chose a skill from a blessing statue.",
        "skill",
        "job",
        "skill_type");

    private void TrackOffered(EntityUid player, IReadOnlyList<ProtoId<CESkillPrototype>> skills)
    {
        var job = ResolveJobLabel(player);
        foreach (var skill in skills)
        {
            SkillsOffered.WithLabels(skill.Id, job, ResolveSkillTypeLabel(skill)).Inc();
        }
    }

    private void TrackChosen(EntityUid player, ProtoId<CESkillPrototype> skill)
    {
        SkillsChosen.WithLabels(skill.Id, ResolveJobLabel(player), ResolveSkillTypeLabel(skill)).Inc();
    }

    private string ResolveJobLabel(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return "unknown";

        if (!_job.MindTryGetJobId(mindId, out var jobId) || jobId is null)
            return "unknown";

        return jobId.Value.Id;
    }

    private string ResolveSkillTypeLabel(ProtoId<CESkillPrototype> skill)
    {
        if (!_proto.TryIndex(skill, out var proto))
            return "unknown";

        return proto.Effect.SkillType.Id;
    }
}
