using Content.Server._CE.Achievements;
using Content.Shared._CE.Achievements.Prototypes;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Restrictions;

public sealed partial class AchievementObtained : CESkillRestriction
{
    [DataField(required: true)]
    public ProtoId<CEAchievementPrototype> Achievement = default!;

    public override bool Check(IEntityManager entManager, EntityUid target)
    {
        var achievementSys = entManager.System<CEAchievementsSystem>();

        // Try to get the player actor component
        if (!entManager.TryGetComponent<ActorComponent>(target, out var actor))
            return false;

        var userId = actor.PlayerSession.UserId;

        if (Inverted)
            return !achievementSys.HasCachedAchievement(userId, Achievement.Id);

        return achievementSys.HasCachedAchievement(userId, Achievement.Id);
    }
}
