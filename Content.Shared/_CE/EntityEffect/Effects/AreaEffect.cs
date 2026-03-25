using Content.Shared.Whitelist;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class AreaEffect : CEEntityEffectBase<AreaEffect>
{
    [DataField(required: true)]
    public List<CEEntityEffect> Effects { get; set; } = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// How many entities can be subject to EntityEffect? Leave 0 to remove the restriction.
    /// </summary>
    [DataField]
    public int MaxTargets;

    [DataField(required: true)]
    public float Range = 1f;

    [DataField]
    public bool AffectCaster;
}

public sealed partial class CEAreaEffectEffectSystem : CEEntityEffectSystem<AreaEffect>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override void Effect(ref CEEntityEffectEvent<AreaEffect> args)
    {
        if (!TryResolveTargetCoordinates(args.Args, out var targetPoint))
            return;

        var entitiesAround = _lookup.GetEntitiesInRange(targetPoint, args.Effect.Range, LookupFlags.Uncontained);

        var count = 0;
        foreach (var entity in entitiesAround)
        {
            if (entity == args.Args.User && !args.Effect.AffectCaster)
                continue;

            if (!_whitelist.CheckBoth(entity, args.Effect.Blacklist, args.Effect.Whitelist))
                continue;

            var nestedArgs = args.Args with { Target = entity, Position = null };
            foreach (var effect in args.Effect.Effects)
            {
                effect.Effect(nestedArgs);
            }

            count++;

            if (args.Effect.MaxTargets > 0 && count >= args.Effect.MaxTargets)
                break;
        }
    }
}
