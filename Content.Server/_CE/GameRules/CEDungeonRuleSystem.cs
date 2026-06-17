using Content.Server._CE.Procedural.Instance;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.Server._CE.GameRules;

public sealed partial class CEDungeonRuleSystem : GameRuleSystem<CEDungeonRuleComponent>
{
    [Dependency] private CEDungeonInstanceSystem _dungeonInstances = default!;

    protected override void Started(
        EntityUid uid,
        CEDungeonRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _dungeonInstances.GenerateRoundstartDungeons();
    }
}
