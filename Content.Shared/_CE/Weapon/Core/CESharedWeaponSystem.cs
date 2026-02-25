
using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Weapon.Core;

public abstract partial class CESharedWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly CESharedAnimationActionSystem AnimationAction = default!;


    private EntityQuery<CEWeaponComponent> _weaponQuery;

    public override void Initialize()
    {
        base.Initialize();

        _weaponQuery = GetEntityQuery<CEWeaponComponent>();

        SubscribeAllEvent<CEWeaponAttackEvent>(OnClientAttackRequest);
    }

    private void OnClientAttackRequest(CEWeaponAttackEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        TryAttack(user, weapon.Value, ev, args.SenderSession, ev.Angle);
    }

    private bool TryAttack(
        EntityUid user,
        Entity<CEWeaponComponent> weapon,
        CEWeaponAttackEvent attackEvent,
        ICommonSession? session,
        Angle angle)
    {
        var curTime = Timing.CurTime;

        if (!CombatMode.IsInCombatMode(user))
            return false;

        if (!Blocker.CanAttack(user))
            return false;

        if (!weapon.Comp.Attacks.TryGetValue(attackEvent.AttackType, out var attackProtoId))
            return false;

        return AnimationAction.TryPlayAnimation(user, attackProtoId, weapon);
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

        // Use in-hands entity if available.
        if (_hands.TryGetActiveItem(entity, out var held) &&
            TryComp<CEWeaponComponent>(held, out var heldWeapon))
        {
            weapon = (held.Value, heldWeapon);
            return true;
        }

        // Use own unarmed melee.
        if (TryComp<CEWeaponComponent>(entity, out var melee))
        {
            weapon = (entity, melee);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether the user is allowed to attack.
    /// Checks container state and raises <see cref="CEAttackAttemptEvent"/>.
    /// </summary>
    public bool CanAttack(EntityUid user, EntityUid? target = null, Entity<CEWeaponComponent>? weapon = null)
    {
        return Blocker.CanAttack(user, target);

        //if (!Blocker.CanAttack(user, target))
        //    return false;
//
        //var ev = new CEAttackAttemptEvent(user, target, weapon);
        //RaiseLocalEvent(user, ev);
//
        //return !ev.Cancelled;
    }
}
