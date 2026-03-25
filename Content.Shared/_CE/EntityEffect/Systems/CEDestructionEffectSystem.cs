using Content.Shared._CE.Health;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.EntityEffect.Systems;

public sealed class CEDestructionEffectSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDestructionEffectComponent, CEDestructedEvent>(OnDestructed);
    }

    private void OnDestructed(Entity<CEDestructionEffectComponent> ent, ref CEDestructedEvent args)
    {
        var entitiesAround = _lookup.GetEntitiesInRange(args.Position, ent.Comp.Range, LookupFlags.Uncontained);

        var count = 0;
        foreach (var entity in entitiesAround)
        {
            if (entity == ent.Owner)
                continue;

            if (!_whitelist.CheckBoth(entity, ent.Comp.Blacklist, ent.Comp.Whitelist))
                continue;

            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                ent.Owner,
                null,
                Angle.Zero,
                0f,
                entity,
                Transform(entity).Coordinates);

            foreach (var effect in ent.Comp.Effects)
            {
                effect.Effect(effectArgs);
            }

            count++;

            if (ent.Comp.MaxTargets > 0 && count >= ent.Comp.MaxTargets)
                break;
        }
    }
}
