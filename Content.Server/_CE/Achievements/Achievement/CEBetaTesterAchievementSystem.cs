using Content.Shared._CE.Achievements.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Achievements.Achievement;

public sealed class CEBetaTesterAchievementSystem : EntitySystem
{
    [Dependency] private readonly CEAchievementsSystem _achievement = default!;

    private ProtoId<CEAchievementPrototype> Proto = "BetaTester";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        _achievement.AddPlayerAchievementAsync(ev.Player.UserId, Proto);
    }
}
