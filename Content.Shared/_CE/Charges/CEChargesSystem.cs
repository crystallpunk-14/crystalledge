using Content.Shared.Examine;

namespace Content.Shared._CE.Charges;

/// <summary>
/// Manages charge spending and restoring for entities with <see cref="CEChargesComponent"/>.
/// </summary>
public sealed class CEChargesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEChargesComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CEChargesComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("ce-charges-status",
            ("current", ent.Comp.CurrentCharges),
            ("max", ent.Comp.MaxCharges)));
    }
    /// <summary>
    /// Returns true if the entity has at least <paramref name="amount"/> charges available.
    /// </summary>
    public bool HasCharges(EntityUid uid, int amount = 1, CEChargesComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        return comp.CurrentCharges >= amount;
    }

    /// <summary>
    /// Tries to spend charges. Returns true if enough charges were available.
    /// </summary>
    public bool TrySpend(EntityUid uid, int amount = 1, CEChargesComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (comp.CurrentCharges < amount)
            return false;

        comp.CurrentCharges -= amount;
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Restores charges by the given amount, clamped to max.
    /// </summary>
    public void Restore(EntityUid uid, int amount, CEChargesComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.CurrentCharges = Math.Min(comp.CurrentCharges + amount, comp.MaxCharges);
        Dirty(uid, comp);
    }

    /// <summary>
    /// Restores charges by a fraction of max charges.
    /// </summary>
    /// <param name="uid">Target entity.</param>
    /// <param name="percentage">Fraction of max charges to restore. 1.0 = 100%.</param>
    /// <param name="comp">Optional resolved component.</param>
    public void RestorePercentage(EntityUid uid, float percentage, CEChargesComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var amount = (int)(comp.MaxCharges * percentage);
        comp.CurrentCharges = Math.Min(comp.CurrentCharges + amount, comp.MaxCharges);
        Dirty(uid, comp);
    }
}
