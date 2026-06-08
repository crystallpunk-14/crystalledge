namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Teleports the resolved entity to the action target coordinates.
/// </summary>
public sealed partial class Teleport : CEEntityEffectBase<Teleport>
{
    public Teleport()
    {
        EffectTarget = CEEffectTarget.User;
    }
}

public sealed partial class CETeleportEffectSystem : CEEntityEffectSystem<Teleport>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Effect(ref CEEntityEffectEvent<Teleport> args)
    {
        // Destination: target entity position first, then cast position
        if (!TryResolveEffectCoordinates(args.Args, CEEffectTarget.Target, out var destination))
            return;

        _transform.SetCoordinates(args.Args.Source, destination);
    }
}
