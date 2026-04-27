using Content.Shared._CE.Frost;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Applies frost/cold slowdown to all entities in an area via <see cref="CEFrostSystem.FreezeArea"/>.
/// </summary>
/// <remarks>OBSOLETE: bypasses the status effect attempt event pipeline (CEAttemptApplyStatusEffectEvent),
/// so immunity components cannot intercept it without workarounds.
/// TODO: migrate to modular AreaEffect + ApplyStatusEffectStack effects.</remarks>
[Obsolete("Use modular AreaEffect with ApplyStatusEffectStack instead. FreezeArea bypasses the status effect attempt pipeline.")]
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
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var targetPoint))
            return;

        _frost.FreezeArea(targetPoint, args.Effect.Radius, args.Effect.FallOffFactor, args.Effect.MaxStacks);
    }
}
