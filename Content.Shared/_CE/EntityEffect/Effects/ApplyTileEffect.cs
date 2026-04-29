using Content.Shared._CE.TileEffects;
using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Spawns or adds stacks to a tile effect entity at the target location
/// via <see cref="CETileEffectSystem.TryApplyTileEffect"/>.
/// </summary>
public sealed partial class ApplyTileEffect : CEEntityEffectBase<ApplyTileEffect>
{
    /// <summary>
    /// The entity prototype to spawn. Must have <see cref="CETileEffectComponent"/>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId TileEffect;

    /// <summary>
    /// Number of stacks to apply.
    /// </summary>
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Per-call stack cap. 0 means use the component's MaxStacks.
    /// </summary>
    [DataField]
    public int Max;
}

public sealed partial class CEApplyTileEffectSystem : CEEntityEffectSystem<ApplyTileEffect>
{
    [Dependency] private readonly CETileEffectSystem _tileEffect = default!;

    protected override void Effect(ref CEEntityEffectEvent<ApplyTileEffect> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var coords))
            return;

        _tileEffect.TryApplyTileEffect(args.Effect.TileEffect, args.Args.Source, coords, args.Effect.Amount, args.Effect.Max);
    }
}
