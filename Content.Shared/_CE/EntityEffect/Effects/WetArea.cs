using Content.Shared._CE.Water;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class WetArea : CEEntityEffectBase<WetArea>
{
    [DataField]
    public float Radius = 3;

    [DataField]
    public float FallOffFactor = 0.5f;

    [DataField]
    public int MaxStacks = 3;
}

public sealed partial class CEWetAreaEffectSystem : CEEntityEffectSystem<WetArea>
{
    [Dependency] private readonly CESharedWaterSystem _water = default!;

    protected override void Effect(ref CEEntityEffectEvent<WetArea> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var targetPoint))
            return;

        _water.WetArea(targetPoint, args.Effect.Radius, args.Effect.FallOffFactor, args.Effect.MaxStacks);
    }
}
