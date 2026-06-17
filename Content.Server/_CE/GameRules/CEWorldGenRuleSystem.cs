using Content.Server._CE.WorldGen;
using Content.Server.GameTicking.Rules;
using Content.Shared.GameTicking.Components;

namespace Content.Server._CE.GameRules;

public sealed partial class CEWorldGenRuleSystem : GameRuleSystem<CEWorldGenRuleComponent>
{
    [Dependency] private CEWorldGenSystem _worldGen = default!;

    protected override void Started(
        EntityUid uid,
        CEWorldGenRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        _worldGen.CreateWorld(component.Config);
    }
}
