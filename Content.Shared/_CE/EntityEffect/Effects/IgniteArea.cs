using Content.Shared._CE.Fire;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class IgniteArea : CEEntityEffectBase<IgniteArea>
{
    [DataField]
    public float Radius = 3;
    [DataField]
    public float FallOffFactor = 0.5f;
    [DataField]
    public int MaxStacks = 10;
}

public sealed partial class CEIgniteAreaEffectSystem : CEEntityEffectSystem<IgniteArea>
{
    [Dependency] private readonly CEFireSystem _fire = default!;

    protected override void Effect(ref CEEntityEffectEvent<IgniteArea> args)
    {
        if (!TryResolveTargetCoordinates(args.Args, out var targetPoint))
            return;

        _fire.IgniteArea(targetPoint, args.Effect.Radius, args.Effect.FallOffFactor, args.Effect.MaxStacks);
    }
}
