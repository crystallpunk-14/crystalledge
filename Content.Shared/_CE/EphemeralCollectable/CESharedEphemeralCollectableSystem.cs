using Content.Shared._CE.Procedural.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._CE.EphemeralCollectable;

/// <summary>
/// Shared, predicted base for the ephemeral-collectable system.
/// Subscribes to <see cref="StartCollideEvent"/> and applies the configured effects to dungeon
/// players that touch the collectable, recording each player exactly once.
/// Also exposes the same collection path via <see cref="InteractHandEvent"/> so a player
/// can click the collectable directly to pick it up.
///
/// Runs on both client and server: client predicts the collection locally so visuals react
/// immediately; the server is authoritative and reconciles state via <c>AutoGenerateComponentState</c>.
/// The <see cref="CEEphemeralCollectableComponent.CollectedBy"/> guard prevents double-application
/// during physics resimulation.
/// </summary>
public abstract class CESharedEphemeralCollectableSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEphemeralCollectableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEEphemeralCollectableComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CEEphemeralCollectableComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CEEphemeralCollectableComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnMapInit(Entity<CEEphemeralCollectableComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CollectableAt = _timing.CurTime + ent.Comp.CollectionDelay;
        Dirty(ent);
    }

    private void OnStartCollide(Entity<CEEphemeralCollectableComponent> ent, ref StartCollideEvent args)
    {
        TryCollect(ent, args.OtherEntity);
    }

    private void OnInteractHand(Entity<CEEphemeralCollectableComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryCollect(ent, args.User))
            args.Handled = true;
    }

    private void OnActivate(Entity<CEEphemeralCollectableComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (TryCollect(ent, args.User))
            args.Handled = true;
    }

    /// <summary>
    /// Applies the collectable's effects to <paramref name="player"/> if they are a dungeon
    /// player and have not already collected this entity. Returns <c>true</c> if collection
    /// happened on this call.
    /// </summary>
    private bool TryCollect(Entity<CEEphemeralCollectableComponent> ent, EntityUid player)
    {
        if (!HasComp<CEDungeonPlayerComponent>(player))
            return false;

        if (ent.Comp.CollectedBy.Contains(player))
            return false;

        // Honour the post-spawn grace period so the player can actually see the drop
        // and the client has time to receive the entity's networked state.
        if (_timing.CurTime < ent.Comp.CollectableAt)
            return false;

        foreach (var effect in ent.Comp.Effects)
        {
            var effectArgs = new EntityEffect.CEEntityEffectArgs(
                EntityManager,
                Source: ent,
                Used: null,
                Angle: Angle.Zero,
                Speed: 0f,
                Target: player,
                Position: null);

            effect.Effect(effectArgs);
        }

        ent.Comp.CollectedBy.Add(player);
        Dirty(ent);

        if (ent.Comp.CollectSound != null)
            _audio.PlayPredicted(ent.Comp.CollectSound, Transform(ent).Coordinates, player);

        var ev = new CEEphemeralCollectedEvent(player);
        RaiseLocalEvent(ent.Owner, ref ev);
        return true;
    }

    public void MarkAsCollectedFor(Entity<CEEphemeralCollectableComponent?> ent, EntityUid player)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (ent.Comp.CollectedBy.Contains(player))
            return;

        ent.Comp.CollectedBy.Add(player);
        Dirty(ent);
    }
}

/// <summary>
/// Raised on a <see cref="CEEphemeralCollectableComponent"/> entity right after a player
/// collected it (effects applied, player added to <c>CollectedBy</c>).
/// Used by the client system to refresh visuals immediately on a predicted local collection,
/// without waiting for server state to arrive.
/// </summary>
[ByRefEvent]
public readonly record struct CEEphemeralCollectedEvent(EntityUid Player);
