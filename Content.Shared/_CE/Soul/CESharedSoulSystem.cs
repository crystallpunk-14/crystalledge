using Content.Shared._CE.EphemeralCollectable;
using Content.Shared._CE.Health;
using Content.Shared._CE.Soul.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Soul;

/// <summary>
/// Public API for reading and modifying the soul count on entities with
/// <see cref="CESoulContainerComponent"/>, and for spending souls into entities
/// with <see cref="CESoulReceiverComponent"/>.
/// All write APIs clamp to <c>[0, MaxSouls]</c> and dirty the component when the value changes.
/// </summary>
public abstract class CESharedSoulSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly CESharedEphemeralCollectableSystem _collectable = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Soul drops happen only on the server: spawning shards from CEDestructedEvent
        // is authoritative world state.
        SubscribeLocalEvent<CESoulDropOnDeathComponent, CEDestructedEvent>(OnDestructed);
        SubscribeLocalEvent<CESoulCapComponent, CESoulReceivedEvent>(OnCapReceived);
        SubscribeLocalEvent<CESoulCollectableComponent, MapInitEvent>(OnCollectableInit);
    }

    private void OnCollectableInit(Entity<CESoulCollectableComponent> ent, ref MapInitEvent args)
    {
        var query = EntityQueryEnumerator<CESoulCapComponent>();
        while (query.MoveNext(out var uid, out var cap))
        {
            if (cap.Current < cap.Cap)
                continue;

            _collectable.MarkAsCollectedFor(ent.Owner, uid);
        }
    }

    private void OnCapReceived(Entity<CESoulCapComponent> ent, ref CESoulReceivedEvent args)
    {
        ent.Comp.Current = Math.Min(ent.Comp.Current + args.Amount, ent.Comp.Cap);
        Dirty(ent);
    }

    private void OnDestructed(Entity<CESoulDropOnDeathComponent> ent, ref CEDestructedEvent args)
    {
        if (!_net.IsServer)
            return;

        var count = ent.Comp.Souls;
        var maxSpeed = ent.Comp.ScatterSpeed;

        for (var i = 0; i < count; i++)
        {
            var soul = Spawn(ent.Comp.Prototype, args.Position);

            // Mirror CEDestructible's scatter: spawn at the death point and impart
            // a random horizontal impulse so souls fan out smoothly via physics.
            _physics.SetLinearVelocity(soul, _random.NextAngle().ToVec() * _random.NextFloat(0.5f, maxSpeed));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only the server completes the transfer and raises the canonical event.
        // Clients only run the visual animation via their dedicated client system.
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<CESoulTransferComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime - comp.StartTime < TimeSpan.FromSeconds(comp.Duration))
                continue;

            var receiverNet = comp.Receiver;
            RemComp<CESoulTransferComponent>(uid);

            if (TryGetEntity(receiverNet, out var receiverUid))
            {
                var ev = new CESoulSpentEvent(uid);
                RaiseLocalEvent(receiverUid.Value, ref ev);
            }
        }
    }

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

        var ev = new CESoulReceivedEvent(ent.Owner, amount);
        RaiseLocalEvent(ent.Owner, ref ev);

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
    /// Attempts to charge <paramref name="player"/> the receiver's soul cost and
    /// start a soul-transfer animation. On success the souls are deducted immediately
    /// and a <see cref="CESoulTransferComponent"/> is added to the player; the
    /// canonical <see cref="CESoulSpentEvent"/> on the receiver is delayed until
    /// the animation finishes (handled in <see cref="Update"/>).
    /// Fails (and shows a predicted popup) if not enough souls. Fails silently if
    /// a transfer is already in progress on the player.
    /// Concurrency/locking on the receiver itself is the consumer's responsibility.
    /// </summary>
    public bool TrySpendSouls(Entity<CESoulReceiverComponent?> ent, EntityUid player)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        // Already transferring souls — block any concurrent attempt by the same player.
        if (HasComp<CESoulTransferComponent>(player))
            return false;

        if (GetSouls(player) < ent.Comp.Cost)
        {
            _popup.PopupEntity(
                Loc.GetString("ce-soul-receiver-not-enough"),
                ent.Owner,
                player);
            return false;
        }

        if (!TryRemoveSouls(player, ent.Comp.Cost))
            return false;

        var transfer = AddComp<CESoulTransferComponent>(player);
        transfer.Receiver = GetNetEntity(ent.Owner);
        transfer.Cost = ent.Comp.Cost;
        transfer.StartTime = _timing.CurTime;
        Dirty(player, transfer);

        return true;
    }
}

/// <summary>
/// Raised on a <see cref="CESoulReceiverComponent"/> entity right after a player
/// successfully spent the configured cost via <see cref="CESharedSoulSystem.TrySpendSouls"/>.
/// </summary>
[ByRefEvent]
public readonly record struct CESoulSpentEvent(EntityUid Player);


[ByRefEvent]
public readonly record struct CESoulReceivedEvent(EntityUid Player, int Amount);
