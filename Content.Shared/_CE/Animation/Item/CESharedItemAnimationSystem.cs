using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Animation.Item;

public abstract partial class CESharedItemAnimationSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] private   readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly CESharedAnimationActionSystem AnimationAction = default!;
    [Dependency] private   readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<CEItemAnimationUseEvent>(OnClientAttackRequest);
    }

    private void OnClientAttackRequest(CEItemAnimationUseEvent ev, EntitySessionEventArgs args)
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
        Entity<CEItemAnimationComponent> weapon,
        CEItemAnimationUseEvent attackEvent,
        ICommonSession? session,
        Angle angle)
    {
        var curTime = Timing.CurTime;

        if (!CombatMode.IsInCombatMode(user))
            return false;

        if (!Blocker.CanAttack(user))
            return false;

        if (!weapon.Comp.Animations.TryGetValue(attackEvent.UseType, out var animations)
            || animations.Count == 0)
            return false;

        // Determine combo index.
        // Reset if: different use type, or combo deadline expired.
        var comboIndex = 0;
        if (weapon.Comp.LastComboUseType == attackEvent.UseType
            && curTime < weapon.Comp.ComboResetDeadline)
        {
            comboIndex = weapon.Comp.ComboIndex % animations.Count;
        }

        var animationProtoId = animations[comboIndex];

        if (!AnimationAction.TryPlayAnimation(user, animationProtoId, weapon.Owner, angle))
            return false;

        // Calculate the deadline: animation duration + configurable delay.
        var animDuration = _proto.Index(animationProtoId).Duration;
        weapon.Comp.LastComboUseType = attackEvent.UseType;
        weapon.Comp.ComboIndex = comboIndex + 1;
        weapon.Comp.ComboResetDeadline = curTime + animDuration + weapon.Comp.ComboResetDelay;
        Dirty(weapon);

        return true;
    }

    public bool TryGetWeapon(EntityUid entity, [NotNullWhen(true)] out Entity<CEItemAnimationComponent>? weapon)
    {
        weapon = null;

        var ev = new CEGetItemAnimationEvent();
        RaiseLocalEvent(entity, ev);
        if (ev.Handled && ev.Weapon != null)
        {
            weapon = ev.Weapon;
            return true;
        }

        // Use in-hands entity if available.
        if (_hands.TryGetActiveItem(entity, out var held) &&
            TryComp<CEItemAnimationComponent>(held, out var heldWeapon))
        {
            weapon = (held.Value, heldWeapon);
            return true;
        }

        // Use own unarmed melee.
        if (TryComp<CEItemAnimationComponent>(entity, out var melee))
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
    public bool CanAttack(EntityUid user, EntityUid? target = null, Entity<CEItemAnimationComponent>? weapon = null)
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
