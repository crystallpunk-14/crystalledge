using Content.Shared.Throwing;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class Dash : CEEntityEffectBase<Dash>
{
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
        _throwing.TryThrow(
            args.Args.User,
            args.Args.Angle.ToWorldVec() * args.Effect.Distance,
            args.Effect.Speed,
            args.Args.User,
            animated: false,
            doSpin: false);
    }
}
