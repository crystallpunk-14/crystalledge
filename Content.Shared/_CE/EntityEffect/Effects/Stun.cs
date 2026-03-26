using Content.Shared.Stunnable;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Stun : CEEntityEffectBase<Stun>
{
    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public bool DropItems = false;
}

public sealed partial class CEStunEffectSystem : CEEntityEffectSystem<Stun>
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    protected override void Effect(ref CEEntityEffectEvent<Stun> args)
    {
        if (args.Args.Target is null)
            return;

        _stun.TryKnockdown(args.Args.Target.Value, args.Effect.Duration, drop: args.Effect.DropItems);
        _stun.TryAddStunDuration(args.Args.Target.Value, args.Effect.Duration);
    }
}
