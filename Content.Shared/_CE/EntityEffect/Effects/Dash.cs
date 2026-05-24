using Content.Shared.Throwing;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Dash : CEEntityEffectBase<Dash>
{
    public Dash()
    {
        EffectTarget = CEEffectTarget.User;
    }

    [DataField]
    public float Speed = 10f;

    [DataField]
    public float Distance = 1f;
}

public sealed partial class CEDashEffectSystem : CEEntityEffectSystem<Dash>
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    protected override void Effect(ref CEEntityEffectEvent<Dash> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _throwing.TryThrow(
            entity,
            args.Args.Angle.ToWorldVec() * args.Effect.Distance,
            args.Effect.Speed,
            entity,
            animated: false,
            doSpin: false);
    }
}
