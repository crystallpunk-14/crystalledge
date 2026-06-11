using Content.Server._CE.Trading;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Trading.Components;

namespace Content.Server._CE.EntityEffect.Effects;

public sealed partial class CERefreshTradingSlotEffectSystem : CEEntityEffectSystem<RefreshTradingSlot>
{
    [Dependency] private CETradingSystem _trading = default!;

    [Dependency] private EntityQuery<CETradingSlotComponent> _slotQuery = default!;

    protected override void Effect(ref CEEntityEffectEvent<RefreshTradingSlot> args)
    {
        var target = ResolveEffectEntity(args.Args, args.Effect.EffectTarget);
        if (target is not { } slotUid)
            return;

        if (!_slotQuery.TryGetComponent(slotUid, out var slot))
            return;

        if (slot.ActiveOffer != null)
            return;

        _trading.RefreshSlot(new Entity<CETradingSlotComponent>(slotUid, slot));
    }
}
