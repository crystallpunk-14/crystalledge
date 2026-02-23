using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Weapon.Core;

public abstract class CESharedWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private   readonly INetManager _netMan = default!;
    [Dependency] private   readonly IPrototypeManager _protoManager = default!;
    [Dependency] private   readonly IRobustRandom _random = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly IComponentFactory CompFactory = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] private   readonly SharedHandsSystem _hands = default!;
    [Dependency] private   readonly InventorySystem _inventory = default!;
    [Dependency] private   readonly MeleeSoundSystem _meleeSound = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;
    [Dependency] private   readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] private   readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private   readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private   readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private   readonly SharedContainerSystem _container = default!;

    private const int AttackMask = (int) (CollisionGroup.MobMask | CollisionGroup.Opaque);

    private EntityQuery<CEWeaponComponent> _weaponQuery;

    public override void Initialize()
    {
        base.Initialize();

        _weaponQuery = GetEntityQuery<CEWeaponComponent>();

        SubscribeLocalEvent<CEWeaponResetOnEquipComponent, HandSelectedEvent>(OnMeleeSelected);

        SubscribeAllEvent<CEStopAttackEvent>(OnStopAttack);
        SubscribeAllEvent<CEWeaponAttackEvent>(OnClientAttackRequest);
    }

    private void OnClientAttackRequest(CEWeaponAttackEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        TryAttack(user, weapon.Value, ev, args.SenderSession);
    }

    private bool TryAttack(EntityUid user, Entity<CEWeaponComponent> weapon, CEWeaponAttackEvent attackEvent, ICommonSession? session)
    {
        var curTime = Timing.CurTime;

        if (weapon.Comp.NextAttack > curTime)
            return false;

        if (!CombatMode.IsInCombatMode(user))
            return false;

        if (!weapon.Comp.Attacks.TryGetValue(attackEvent.AttackType, out var attackProto))
            return false;

        if (!_protoManager.Resolve(attackProto, out var resolvedAttackProto))
            return false;

        var fireRate = TimeSpan.FromSeconds(1f / attack.AttackRate);
        var swings = 0;

        if (weapon.Comp.NextAttack < curTime)
            weapon.Comp.NextAttack = curTime;

        while (weapon.Comp.NextAttack <= curTime)
        {
            weapon.Comp.NextAttack += fireRate;
            swings++;
        }

        DirtyField(weapon, weapon.Comp, nameof(CEWeaponComponent.NextAttack));

        // Do this AFTER attack so it doesn't spam every tick
        var ev = new CEWeaponAttackAttemptEvent(user, weapon);
        RaiseLocalEvent(weapon, ref ev);

        //TODO: Swing beverage here

        if (ev.Cancelled)
            return false;

        //Attack Confirmed
        for (var i = 0; i < swings; i++)
        {
            DoAttack(user, weapon, attackEvent, session);
            //TODO: Lunge animation?
        }

        var afterAttackEvent = new CEAfterAttackEvent(weapon);
        RaiseLocalEvent(user, ref afterAttackEvent);

        weapon.Comp.Attacking = true;
        DirtyField(weapon, weapon.Comp, nameof(CEWeaponComponent.Attacking));
        return true;
    }

    private bool DoAttack(EntityUid user, Entity<CEWeaponComponent> weapon, CEWeaponAttackEvent ev, ICommonSession? session)
    {
        if (!TryComp(user, out TransformComponent? userXform))
            return false;


    }

    private void OnMeleeSelected(Entity<CEWeaponResetOnEquipComponent> ent, ref HandSelectedEvent args)
    {
        if (Paused(ent))
            return;

        if(!_weaponQuery.TryComp(ent, out var weaponComp))
            return;

        weaponComp.NextAttack = Timing.CurTime + ent.Comp.Cooldown;
        DirtyField(ent, weaponComp, nameof(CEWeaponComponent.NextAttack));
    }

    public bool TryGetWeapon(EntityUid entity, [NotNullWhen(true)] out Entity<CEWeaponComponent>? weapon)
    {
        weapon = null;

        var ev = new CEGetMeleeWeaponEvent();
        RaiseLocalEvent(entity, ev);
        if (ev.Handled && ev.Weapon != null)
        {
            weapon = ev.Weapon;
            return true;
        }

        // Use inhands entity if we got one.
        if (_hands.TryGetActiveItem(entity, out var held) && TryComp<CEWeaponComponent>(held, out var heldWeapon))
        {
            weapon = (held.Value, heldWeapon);
            return true;
        }

        // Use our own melee
        if (TryComp<CEWeaponComponent>(entity, out var melee))
        {
            weapon = (entity, melee);
            return true;
        }

        return false;
    }

    public bool CanAttack(EntityUid user, EntityUid? target = null, Entity<CEWeaponComponent>? weapon = null, bool disarm = false)
    {
        // If target is in a container can we attack
        if (target != null && _container.IsEntityInContainer(target.Value))
        {
            return false;
        }

        _container.TryGetOuterContainer(user, Transform(user), out var outerContainer);

        // If we're in a container can we attack the target.
        if (target != null && target != outerContainer?.Owner && _container.IsEntityInContainer(user))
        {
            var containerEv = new CanAttackFromContainerEvent(user, target);
            RaiseLocalEvent(user, containerEv);
            if (!containerEv.CanAttack)
                return false;
        }

        var ev = new CEAttackAttemptEvent(user, target, weapon);
        RaiseLocalEvent(user, ev);

        return ev.Cancelled;
    }

    /// <summary>
    /// CEStopAttackEvent вызывается с клиента как предикт ивент, чтобы при отжатии клавиши атаки завершить атаку.
    /// </summary>
    /// <param name="ev"></param>
    /// <param name="args"></param>
    private void OnStopAttack(CEStopAttackEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (!TryGetWeapon(user.Value, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        if (weapon.Value.Comp.Attacking)
            return;

        weapon.Value.Comp.Attacking = false;
        DirtyField(weapon.Value.Owner, weapon.Value.Comp, nameof(CEWeaponComponent.Attacking));
    }
}
