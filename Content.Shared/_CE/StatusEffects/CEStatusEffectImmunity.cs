using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// When present on a status effect entity, blocks application of the listed status effects
/// to the entity that owns this status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEStatusEffectImmunityComponent : Component
{
    /// <summary>
    /// The list of status effect prototype IDs that are blocked.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> BlockedEffects = new();
}

public sealed class CEStatusEffectImmunitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectEvent>>(OnReceive);
        SubscribeLocalEvent<CEStatusEffectImmunityComponent, StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectStackEvent>>(OnReceiveStack);
    }

    private void OnReceive(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }

    private void OnReceiveStack(Entity<CEStatusEffectImmunityComponent> ent, ref StatusEffectRelayedEvent<CEAttemptReceiveStatusEffectStackEvent> args)
    {
        if (!ent.Comp.BlockedEffects.Contains(args.Args.StatusEffect))
            return;

        args.Args.Cancelled = true;
    }
}
