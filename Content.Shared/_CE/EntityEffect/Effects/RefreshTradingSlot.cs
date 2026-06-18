namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Restocks an inactive trading slot with a new random offer.
/// Server-side logic is handled by <c>CERefreshTradingSlotEffectSystem</c>.
/// </summary>
public sealed partial class RefreshTradingSlot : CEEntityEffectBase<RefreshTradingSlot>
{
    public RefreshTradingSlot()
    {
        EffectTarget = CEEffectTarget.Target;
    }
}
