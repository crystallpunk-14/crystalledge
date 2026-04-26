namespace Content.Shared._CE.Soul;

/// <summary>
/// Public API for reading and modifying the soul count on entities with
/// <see cref="Components.CESoulContainerComponent"/>.
/// All write APIs clamp to <c>[0, MaxSouls]</c> and dirty the component when the value changes.
/// </summary>
public sealed class CESoulSystem : EntitySystem
{
    /// <summary>
    /// Returns the current soul count, or 0 if the entity has no container.
    /// </summary>
    public int GetSouls(EntityUid uid, Components.CESoulContainerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return 0;
        return comp.Souls;
    }

    /// <summary>
    /// Returns the maximum soul count, or 0 if the entity has no container.
    /// </summary>
    public int GetMaxSouls(EntityUid uid, Components.CESoulContainerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return 0;
        return comp.MaxSouls;
    }

    /// <summary>
    /// Sets the soul count to <paramref name="amount"/> (clamped to <c>[0, MaxSouls]</c>).
    /// Returns false if the entity has no container.
    /// </summary>
    public bool TrySetSouls(EntityUid uid, int amount, Components.CESoulContainerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        var clamped = Math.Clamp(amount, 0, comp.MaxSouls);
        if (clamped == comp.Souls)
            return true;

        comp.Souls = clamped;
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> souls (clamped at <c>MaxSouls</c>).
    /// Returns false if the entity has no container.
    /// </summary>
    public bool TryAddSouls(EntityUid uid, int amount, Components.CESoulContainerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        return TrySetSouls(uid, comp.Souls + amount, comp);
    }

    /// <summary>
    /// Removes <paramref name="amount"/> souls if there are enough.
    /// Returns false if the entity has no container or has fewer than <paramref name="amount"/> souls.
    /// </summary>
    public bool TryRemoveSouls(EntityUid uid, int amount, Components.CESoulContainerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (amount < 0)
            return false;

        if (comp.Souls < amount)
            return false;

        return TrySetSouls(uid, comp.Souls - amount, comp);
    }
}
