using Content.Shared._CE.EntityEffect;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Consumable;

public sealed class CEConsumableSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEConsumableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CEConsumableComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CEConsumableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<CEConsumableComponent, CEUseConsumableDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(Entity<CEConsumableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_whitelist.CheckBoth(args.User, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        if (TryStartDoAfter(ent, args.User, args.User, ent.Comp.UseDelay))
            args.Handled = true;
    }

    private void OnAfterInteract(Entity<CEConsumableComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        var delay = ent.Comp.UseDelay;
        if (target != args.User)
            delay *= ent.Comp.OtherUseDelayMultiplier;

        if (TryStartDoAfter(ent, args.User, target, delay))
            args.Handled = true;
    }

    private void OnGetAltVerbs(Entity<CEConsumableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!_whitelist.CheckBoth(args.User, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        var user = args.User;
        var held = _hands.IsHolding(user, ent, out _);

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStartDoAfter(ent, user, user, ent.Comp.UseDelay, needHand: held),
            Text = Loc.GetString("ce-consumable-verb-consume"),
            Priority = 2,
        });
    }

    private bool TryStartDoAfter(Entity<CEConsumableComponent> ent, EntityUid user, EntityUid target, TimeSpan delay, bool needHand = true)
    {
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            delay,
            new CEUseConsumableDoAfterEvent(),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = !needHand,
            BreakOnDamage = true,
            NeedHand = needHand,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<CEConsumableComponent> ent, ref CEUseConsumableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        args.Handled = true;

        _audio.PlayPredicted(ent.Comp.UseSound, Transform(ent).Coordinates, args.User);

        // Apply all effects to the target.
        var effectArgs = new CEEntityEffectArgs(
            EntityManager,
            target,
            ent.Owner,
            Angle.Zero,
            0f,
            target,
            Transform(target).Coordinates);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }

        // Item is depleted (or single-use without charges) — spawn replacement and delete.
        SpawnReplacement(ent, args.User);
        PredictedQueueDel(ent.Owner);
    }

    private void SpawnReplacement(Entity<CEConsumableComponent> ent, EntityUid? user)
    {
        if (ent.Comp.ReplacementEntity is not { } replacement)
            return;

        var position = _transform.GetMapCoordinates(ent);

        // Case 1: item is in a hand — put replacement in the same hand slot.
        string? handId = null;
        if (user != null && _hands.IsHolding(user.Value, ent, out handId))
        {
            var spawned = EntityManager.PredictedSpawn(replacement, position);
            // Free the holding hand without triggering drop interactions (item is about to be deleted).
            _hands.TryDrop(user.Value, ent.Owner, checkActionBlocker: false, doDropInteraction: false);
            _hands.TryPickup(user.Value, spawned, handId);
            return;
        }

        // Case 2: item is in a container (pocket, bag, chest) — put replacement in the same container.
        if (_containers.TryGetContainingContainer(ent.Owner, out var container))
        {
            var spawned = EntityManager.PredictedSpawn(replacement, position);
            _containers.Remove(ent.Owner, container);
            _containers.Insert(spawned, container);
            return;
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class CEUseConsumableDoAfterEvent : SimpleDoAfterEvent;
