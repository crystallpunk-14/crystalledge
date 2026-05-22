using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Relays upstream status-effect events (raised on the effect entity) as CE events on the target entity,
/// so other systems can subscribe per-component on the target.
/// </summary>
public sealed partial class CEStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StatusEffectComponent, StatusEffectAppliedEvent>(OnUpstreamApplied);
        SubscribeLocalEvent<StatusEffectComponent, StatusEffectRemovedEvent>(OnUpstreamRemoved);
    }

    private void OnUpstreamApplied(Entity<StatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        var ev = new CEStatusEffectAppliedToEntityEvent(ent);
        RaiseLocalEvent(args.Target, ref ev);
    }

    private void OnUpstreamRemoved(Entity<StatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var ev = new CEStatusEffectRemovedFromEntityEvent(ent);
        RaiseLocalEvent(args.Target, ref ev);
    }
}


/// <summary>
/// Raised on the <b>target entity</b> (the mob) when a status effect is applied to it.
/// Relayed from the upstream <see cref="StatusEffectAppliedEvent"/> which fires on the effect entity.
/// </summary>
[ByRefEvent]
public readonly record struct CEStatusEffectAppliedToEntityEvent(EntityUid EffectEntity);

/// <summary>
/// Raised on the <b>target entity</b> (the mob) when a status effect is removed from it.
/// Relayed from the upstream <see cref="StatusEffectRemovedEvent"/> which fires on the effect entity.
/// </summary>
[ByRefEvent]
public readonly record struct CEStatusEffectRemovedFromEntityEvent(EntityUid EffectEntity);
