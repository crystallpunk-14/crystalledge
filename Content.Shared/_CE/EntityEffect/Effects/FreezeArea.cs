using Content.Shared._CE.Frost;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class FreezeArea : CEEntityEffectBase<FreezeArea>
{
    [DataField]
    public float Radius = 3;

    [DataField]
    public float FallOffFactor = 0.5f;

    [DataField]
    public int MaxStacks = 3;
}

public sealed partial class CEFreezeAreaEffectSystem : CEEntityEffectSystem<FreezeArea>
{
    [Dependency] private readonly CEFrostSystem _frost = default!;

    protected override void Effect(ref CEEntityEffectEvent<FreezeArea> args)
    {
        if (!TryResolveTargetCoordinates(args.Args, out var targetPoint))
            return;

        _frost.FreezeArea(targetPoint, args.Effect.Radius, args.Effect.FallOffFactor, args.Effect.MaxStacks);
    }
}
