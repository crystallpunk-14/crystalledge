using System.Threading.Tasks;
using Content.Server._CE.Achievements.Components;
using Content.Shared._CE.Health;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._CE.Achievements;

public sealed class CEAchievementOnDestructSystem : EntitySystem
{
    [Dependency] private readonly CEAchievementsSystem _achievements = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEAchievementOnDestructComponent, CEDestructedEvent>(OnDestructed);
    }

    private void OnDestructed(Entity<CEAchievementOnDestructComponent> ent, ref CEDestructedEvent args)
    {
        // Don't award achievements in integration tests to avoid interfering with test cleanup.
        if (_cfg.GetCVar(CCVars.DatabaseSynchronous))
            return;

        if (args.Source is not { } source || !TryComp<ActorComponent>(source, out var actor))
            return;

        _ = _achievements.AddPlayerAchievementAsync(actor.PlayerSession.UserId, ent.Comp.Achievement);
    }
}
