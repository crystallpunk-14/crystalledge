using System.Linq;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared._CE.Procedural.Components;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._CE.Bonfire;

public abstract partial class CESharedBonfireSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private CESharedDamageableSystem _damageable = default!;
    [Dependency] private CESharedMagicEnergySystem _magicEnergy = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBonfireComponent, ActivateInWorldEvent>(OnActivate);

        SubscribeLocalEvent<CEDamageableComponent, CEBonfireRestoredEvent>(OnRestoreHealth);
        SubscribeLocalEvent<CEMagicEnergyContainerComponent, CEBonfireRestoredEvent>(OnRestoreMana);
        SubscribeLocalEvent<ContainerManagerComponent, CEBonfireRestoredEvent>(OnRelayToContents);
    }

    private void OnActivate(Entity<CEBonfireComponent> ent, ref ActivateInWorldEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Handled)
            return;

        var player = args.User;

        if (!HasComp<CEDungeonPlayerComponent>(player))
            return;

        if (ent.Comp.UsedBy.Contains(player))
            return;

        ent.Comp.UsedBy.Add(player);
        Dirty(ent);

        args.Handled = true;
        RaiseLocalEvent(player, new CEBonfireRestoredEvent());

        var coords = Transform(player).Coordinates;

        if (ent.Comp.HealVfx is { } vfx)
            SpawnAtPosition(vfx, coords);

        if (ent.Comp.HealSound is { } sound)
            _audio.PlayPvs(sound, coords);
    }

    private void OnRestoreHealth(Entity<CEDamageableComponent> ent, ref CEBonfireRestoredEvent args)
    {
        var currentDamage = ent.Comp.Damage.Total;
        if (currentDamage <= 0)
            return;

        _damageable.Heal(ent.Owner, currentDamage);
    }

    private void OnRestoreMana(Entity<CEMagicEnergyContainerComponent> ent, ref CEBonfireRestoredEvent args)
    {
        var restoreAmount = ent.Comp.MaxEnergy;
        if (restoreAmount <= 0)
            return;

        _magicEnergy.Restore(ent.Owner, restoreAmount);
    }
    /// <summary>
    /// Relays <see cref="CEBonfireRestoredEvent"/> to every entity stored inside this
    /// container manager. Recursion is natural: if a contained entity also has a
    /// <see cref="ContainerManagerComponent"/> (e.g. a backpack), this handler fires
    /// again on that entity, propagating the event through the entire nesting chain.
    /// </summary>
    private void OnRelayToContents(Entity<ContainerManagerComponent> ent, ref CEBonfireRestoredEvent args)
    {
        foreach (var container in _containers.GetAllContainers(ent))
        {
            foreach (var item in container.ContainedEntities.ToArray())
            {
                RaiseLocalEvent(item, new CEBonfireRestoredEvent());
            }
        }
    }
}

/// <summary>
/// Raised on a player entity when they successfully use a bonfire for the first time.
/// </summary>
public sealed partial class CEBonfireRestoredEvent : EntityEventArgs;
