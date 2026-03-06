using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP.TargetProviders;

/// <summary>
/// Target provider that resolves to the NPC entity itself.
/// Useful for actions that target the caster (e.g. self-heals, defensive abilities).
/// </summary>
public sealed partial class CEGOAPSelfTargetProvider
    : CEGOAPTargetProviderBase<CEGOAPSelfTargetProvider>
{
}

public sealed partial class CEGOAPSelfTargetProviderSystem
    : CEGOAPTargetProviderSystem<CEGOAPSelfTargetProvider>
{
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    protected override void OnResolve(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPResolveTargetEvent<CEGOAPSelfTargetProvider> args)
    {
        args.TargetEntity = ent.Owner;

        if (_xformQuery.TryGetComponent(ent, out var xform))
            args.TargetCoordinates = xform.Coordinates;
    }
}
