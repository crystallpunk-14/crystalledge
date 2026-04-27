using Content.Shared._CE.Fire;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Applies fire stacks to all entities in an area via <see cref="CEFireSystem.IgniteArea"/>.
/// </summary>
/// <remarks>OBSOLETE: bypasses the status effect attempt event pipeline (CEAttemptApplyStatusEffectEvent),
/// so immunity components cannot intercept it without workarounds.
/// TODO: migrate to modular AreaEffect + ApplyStatusEffectStack effects.</remarks>
[Obsolete("Use modular AreaEffect with ApplyStatusEffectStack instead. IgniteArea bypasses the status effect attempt pipeline.")]
public sealed partial class IgniteArea : CEEntityEffectBase<IgniteArea>
{
    [DataField]
    public float Radius = 3;
    [DataField]
    public float FallOffFactor = 0.5f;
    [DataField]
    public int MaxStacks = 3;
}

public sealed partial class CEIgniteAreaEffectSystem : CEEntityEffectSystem<IgniteArea>
{
    [Dependency] private readonly CEFireSystem _fire = default!;

    protected override void Effect(ref CEEntityEffectEvent<IgniteArea> args)
    {
        if (!TryResolveEffectCoordinates(args.Args, args.Effect.EffectTarget, out var targetPoint))
            return;

        _fire.IgniteArea(targetPoint, args.Effect.Radius, args.Effect.FallOffFactor, args.Effect.MaxStacks);
    }
}
