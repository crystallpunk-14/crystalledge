using Content.Shared._CE.Soul.Components;
using Content.Shared.Popups;

namespace Content.Shared._CE.Soul;

/// <summary>
/// Public API for reading and modifying the soul count on entities with
/// <see cref="CESoulContainerComponent"/>, and for spending souls into entities
/// with <see cref="CESoulReceiverComponent"/>.
/// All write APIs clamp to <c>[0, MaxSouls]</c> and dirty the component when the value changes.
/// </summary>
public sealed class CESoulSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    /// Returns the current soul count, or 0 if the entity has no container.
    /// </summary>
    public int GetSouls(Entity<CESoulContainerComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return 0;
        return ent.Comp.Souls;
    }

    /// <summary>
    /// Returns the maximum soul count, or 0 if the entity has no container.
    /// </summary>
    public int GetMaxSouls(Entity<CESoulContainerComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return 0;
        return ent.Comp.MaxSouls;
    }

    /// <summary>
    /// Sets the soul count to <paramref name="amount"/> (clamped to <c>[0, MaxSouls]</c>).
    /// Returns false if the entity has no container.
    /// </summary>
    public bool TrySetSouls(Entity<CESoulContainerComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        var clamped = Math.Clamp(amount, 0, ent.Comp.MaxSouls);
        if (clamped == ent.Comp.Souls)
            return true;

        ent.Comp.Souls = clamped;
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> souls (clamped at <c>MaxSouls</c>).
    /// Returns false if the entity has no container.
    /// </summary>
    public bool TryAddSouls(Entity<CESoulContainerComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        return TrySetSouls((ent.Owner, ent.Comp), ent.Comp.Souls + amount);
    }

    /// <summary>
    /// Removes <paramref name="amount"/> souls if there are enough.
    /// Returns false if the entity has no container or has fewer than <paramref name="amount"/> souls.
    /// </summary>
    public bool TryRemoveSouls(Entity<CESoulContainerComponent?> ent, int amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        if (amount < 0)
            return false;

        if (ent.Comp.Souls < amount)
            return false;

        return TrySetSouls((ent.Owner, ent.Comp), ent.Comp.Souls - amount);
    }

    /// <summary>
    /// Attempts to charge <paramref name="player"/> the receiver's soul cost.
    /// On success the souls are removed and a <see cref="CESoulReceivedEvent"/> is
    /// raised on the receiver entity. On failure (not enough souls) a predicted
    /// popup is shown to the player and no souls are removed.
    /// Concurrency/locking is the consumer's responsibility — this method does not
    /// track which player is "active" on the receiver.
    /// </summary>
    public bool TrySpendSouls(Entity<CESoulReceiverComponent?> ent, EntityUid player)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        if (GetSouls(player) < ent.Comp.Cost)
        {
            _popup.PopupClient(
                Loc.GetString("ce-soul-receiver-not-enough", ("cost", ent.Comp.Cost)),
                ent.Owner,
                player);
            return false;
        }

        if (!TryRemoveSouls(player, ent.Comp.Cost))
            return false;

        var ev = new CESoulReceivedEvent(player);
        RaiseLocalEvent(ent.Owner, ref ev);
        return true;
    }
}

/// <summary>
/// Raised on a <see cref="CESoulReceiverComponent"/> entity right after a player
/// successfully spent the configured cost via <see cref="CESoulSystem.TrySpendSouls"/>.
/// </summary>
[ByRefEvent]
public readonly record struct CESoulReceivedEvent(EntityUid Player);

