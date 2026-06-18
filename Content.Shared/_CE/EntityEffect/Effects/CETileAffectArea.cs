using Content.Shared._CE.TileEffects;
using Content.Shared._CE.TileEffects.Core;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Applies a tile effect to all tiles in an area with optional LOS checking and distance-based stack falloff.
/// The heavy logic lives in <see cref="CETileEffectSystem.ApplyTileEffectArea"/>.
/// </summary>
public sealed partial class CETileAffectArea : CEEntityEffectBase<CETileAffectArea>
{
    /// <summary>
    /// Tile effect entity prototype to spawn or stack on affected tiles.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId TileEffect;

    /// <summary>
    /// Radius in world units.
    /// </summary>
    [DataField]
    public float Radius = 3f;

    /// <summary>
    /// Distance falloff exponent. Higher values concentrate stacks near the center.
    /// </summary>
    [DataField]
    public float FallOffFactor = 0.5f;

    /// <summary>
    /// Maximum stacks to apply at the center tile. Tiles farther away receive fewer stacks.
    /// </summary>
    [DataField]
    public int MaxStacks = 10;

    /// <summary>
    /// Whether to skip tiles that are not in line-of-sight of the effect center.
    /// </summary>
    [DataField]
    public bool CheckLos = true;
}

public sealed partial class CETileAffectAreaSystem : CEEntityEffectSystem<CETileAffectArea>
{
    [Dependency] private CETileEffectSystem _tileEffect = default!;

    protected override void Effect(ref CEEntityEffectEvent<CETileAffectArea> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var targetPoint))
            return;

        _tileEffect.ApplyTileEffectArea(
            args.Effect.TileEffect,
            args.Args.Source,
            targetPoint,
            args.Effect.Radius * args.Args.Power,
            args.Effect.FallOffFactor,
            Math.Max(1, (int)(args.Effect.MaxStacks * args.Args.Power)),
            args.Effect.CheckLos);
    }
}
