using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Achievements;
using Content.Shared._CE.Achievements.Prototypes;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Roles.JobRequirement;

/// <summary>
/// Requires a player to have (or not have) a specific achievement.
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class CEAchievementRequirement : Shared.Roles.JobRequirement
{
    [DataField(required: true)]
    public ProtoId<CEAchievementPrototype> Achievement;

    public override bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason,
        NetUserId? userId = null)
    {
        var achievementName = Achievement.Id;
        if (protoManager.TryIndex(Achievement, out var achievementPrototype))
            achievementName = Loc.GetString(achievementPrototype.Name);

        var hasAchievement = false;
        if (userId != null)
        {
            var achievementManager = IoCManager.Resolve<ICEAchievementsRequirementManager>();
            hasAchievement = achievementManager.HasAchievement(userId.Value, Achievement);
        }

        if (!Inverted)
        {
            reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
                "role-timer-achievement-required",
                ("achievement", achievementName)));
            return hasAchievement;
        }

        reason = FormattedMessage.FromMarkupPermissive(Loc.GetString(
            "role-timer-achievement-forbidden",
            ("achievement", achievementName)));
        return !hasAchievement;
    }
}
