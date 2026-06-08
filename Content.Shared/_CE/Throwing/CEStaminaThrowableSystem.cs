using Content.Shared._CE.Stamina;
using Content.Shared.Throwing;

namespace Content.Shared._CE.Throwing;

public sealed partial class CEStaminaThrowableSystem : EntitySystem
{
    [Dependency] private CEStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStaminaThrowableComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
    }

    private void OnThrowAttempt(Entity<CEStaminaThrowableComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_stamina.TryTakeDamage(args.User, ent.Comp.Cost))
            args.Cancelled = true;
    }
}
