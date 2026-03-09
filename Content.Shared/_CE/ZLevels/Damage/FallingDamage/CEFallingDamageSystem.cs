/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.Health;

namespace Content.Shared._CE.ZLevels.Damage.FallingDamage;

public sealed class CEFallingDamageSystem : EntitySystem
{
    [Dependency] private readonly CESharedHealthSystem _health = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallingDamageComponent, CEZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<CEFallingDamageComponent> ent, ref CEZFellOnMeEvent args)
    {
        _health.TakeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
