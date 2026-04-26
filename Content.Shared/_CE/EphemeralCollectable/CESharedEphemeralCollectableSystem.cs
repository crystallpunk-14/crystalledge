using Content.Shared._CE.Procedural.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._CE.EphemeralCollectable;

/// <summary>
/// Shared, predicted base for the ephemeral-collectable system.
/// Subscribes to <see cref="StartCollideEvent"/> and applies the configured effects to dungeon
/// players that touch the collectable, recording each player exactly once.
///
/// Runs on both client and server: client predicts the collection locally so visuals react
/// immediately; the server is authoritative and reconciles state via <c>AutoGenerateComponentState</c>.
/// The <see cref="CEEphemeralCollectableComponent.CollectedBy"/> guard prevents double-application
/// during physics resimulation.
/// </summary>
public abstract class CESharedEphemeralCollectableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEphemeralCollectableComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<CEEphemeralCollectableComponent> ent, ref StartCollideEvent args)
    {
        var player = args.OtherEntity;

        if (!HasComp<CEDungeonPlayerComponent>(player))
            return;

        if (ent.Comp.CollectedBy.Contains(player))
            return;

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

        var ev = new CEEphemeralCollectedEvent(player);
        RaiseLocalEvent(ent.Owner, ref ev);
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
