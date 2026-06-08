using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Applies a permanent status effect at MapInit whose components are defined inline
/// via <see cref="Components"/>, avoiding the need for a dedicated per-mob prototype.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEInnateStatusEffectComponent : Component
{
    [DataField(required: true), AlwaysPushInheritance]
    public ComponentRegistry Components = new();
}

public sealed partial class CEInnateStatusEffectSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly EntProtoId InnateEffectProto = "CEInnateStatusEffect";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEInnateStatusEffectComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEInnateStatusEffectComponent, ComponentRemove>(OnRemove);
    }

    private void OnMapInit(Entity<CEInnateStatusEffectComponent> ent, ref MapInitEvent args)
    {
        if (!_statusEffects.TrySetStatusEffectDuration(ent, InnateEffectProto, out var effectEnt))
            return;

        EntityManager.AddComponents(effectEnt.Value, ent.Comp.Components);

        // Re-raise so components added above receive their on-apply logic,
        // since StatusEffectAppliedEvent fired before AddComponents ran.
        var statusComp = Comp<StatusEffectComponent>(effectEnt.Value);
        if (statusComp.AppliedTo != null)
        {
            var ev = new StatusEffectAppliedEvent(statusComp.AppliedTo.Value);
            RaiseLocalEvent(effectEnt.Value, ref ev);
        }
    }

    private void OnRemove(Entity<CEInnateStatusEffectComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;
        _statusEffects.TryRemoveStatusEffect(ent, InnateEffectProto);
    }
}
