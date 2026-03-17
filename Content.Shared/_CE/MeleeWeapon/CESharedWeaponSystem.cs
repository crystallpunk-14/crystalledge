using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Health;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Animation.Item;

public abstract partial class CESharedWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] private   readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly CESharedAnimationActionSystem AnimationAction = default!;
    [Dependency] private   readonly IPrototypeManager _proto = default!;
    [Dependency] private   readonly CESharedDamageableSystem _damageable = default!;
    [Dependency] private   readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<CEWeaponUseEvent>(OnClientAttackRequest);
        SubscribeAllEvent<CEStopWeaponUseEvent>(OnClientStopRequest);

        SubscribeLocalEvent<CEWieldedWeaponComponent, CEGetWeaponEvent>(OnGetWeapon);
    }

    private void OnGetWeapon(Entity<CEWieldedWeaponComponent> ent, ref CEGetWeaponEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<WieldableComponent>(ent, out var wielded))
            return;

        if (!wielded.Wielded)
            return;

        if (!ent.Comp.Animations.TryGetValue(args.UseType, out var animations))
            return;

        args.Animations = animations;
        args.Handled = true;
    }

    private void OnClientStopRequest(CEStopWeaponUseEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (!TryGetWeapon(user.Value, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        if (!weapon.Value.Comp.Using)
            return;

        weapon.Value.Comp.Using = false;
        DirtyField(weapon.Value.Owner, weapon.Value.Comp, nameof(CEWeaponComponent.Using));
    }

    private void OnClientAttackRequest(CEWeaponUseEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        TryUse(user, weapon.Value, ev.UseType, ev.Angle);
    }

    public bool TryUse(
        EntityUid user,
        CEUseType useType,
        Angle angle)
    {
        if (!TryGetWeapon(user, out var weapon))
            return false;

        return TryUse(user, weapon.Value, useType, angle);
    }

    public bool TryUse(
        EntityUid user,
        Entity<CEWeaponComponent> used,
        CEUseType useType,
        Angle angle)
    {
        var curTime = Timing.CurTime;

        if (!Blocker.CanAttack(user))
            return false;

        //Get animations
        List<CEAnimationEntry> animations = new();

        var animEv = new CEGetWeaponEvent(used, useType);
        RaiseLocalEvent(used, animEv);

        if (animEv.Handled && animEv.Animations.Count != 0)
            animations = animEv.Animations;
        else //Get default animations
        {
            if (used.Comp.Animations.TryGetValue(useType, out var a))
                animations = a;
        }

        if (animations.Count == 0)
            return false;

        // Determine combo index.
        // Reset if: different use type, or combo deadline expired.
        var comboIndex = 0;
        if (used.Comp.LastComboUseType == useType && curTime < used.Comp.ComboResetDeadline)
            comboIndex = used.Comp.ComboIndex % animations.Count;

        var animationProtoId = animations[comboIndex].Anim;

        var animationSpeed = GetAnimationSpeed(user, used) * animations[comboIndex].Speed;
        if (!AnimationAction.TryPlayAnimationToAngle(user, animationProtoId, angle, used.Owner, animationSpeed))
            return false;

        // Calculate the deadline: animation duration + configurable delay.
        var animDuration = _proto.Index(animationProtoId).Duration;
        used.Comp.LastComboUseType = useType;
        used.Comp.ComboIndex = comboIndex + 1;
        used.Comp.ComboResetDeadline = curTime + (animDuration * animationSpeed) + used.Comp.ComboResetDelay;
        used.Comp.Using = true;
        Dirty(used);

        return true;
    }

    public bool TryGetWeapon(EntityUid entity, [NotNullWhen(true)] out Entity<CEWeaponComponent>? used)
    {
        used = null;

        var ev = new CEGetAnimationItemForUseEvent();
        RaiseLocalEvent(entity, ev);
        if (ev.Handled && ev.Used != null)
        {
            used = ev.Used;
            return true;
        }

        // Use in-hands entity if available.
        if (_hands.TryGetActiveItem(entity, out var held) &&
            TryComp<CEWeaponComponent>(held, out var heldWeapon))
        {
            used = (held.Value, heldWeapon);
            return true;
        }

        // Use own body.
        if (TryComp<CEWeaponComponent>(entity, out var melee))
        {
            used = (entity, melee);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the animation playback speed, where 1 = 100% speed, 2 = 200% speed
    /// </summary>
    private float GetAnimationSpeed(EntityUid entity, Entity<CEWeaponComponent> used)
    {
        var ev = new CEGetWeaponSpeedEvent();
        RaiseLocalEvent(entity, ev);
        RaiseLocalEvent(used, ev);

        var speed = ev.GetSpeed();
        return speed;
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

    public bool TryAttack(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, float power)
    {
        List<EntityUid> hitted = new();
        foreach (var target in targets)
        {
            if (!_damageable.TakeDamage(target, weapon.Comp.Damage * power, user))
                continue;

            var attackedEv = new CEAttackedEvent(user, weapon);
            RaiseLocalEvent(target, attackedEv);

            hitted.Add(target);
        }

        if (!hitted.Any())
            return false;

        //Attack confirmed

        RaiseAttackEffects(user, hitted);
        _audio.PlayPredicted(weapon.Comp.HitSound, weapon, user);

        var usedEv = new CEAttackUsingEvent(user, hitted);
        RaiseLocalEvent(weapon, usedEv);

        var attackerEv = new CEAfterAttackEvent(weapon, hitted);
        RaiseLocalEvent(user, attackerEv);

        return true;
    }

    /// <summary>
    /// Override this method in client/server implementations to handle visual effects.
    /// </summary>
    protected virtual void RaiseAttackEffects(EntityUid user, List<EntityUid> targets)
    {
        // Base implementation does nothing - effects are handled in client/server implementations
    }
}

/// <summary>
/// Raised on used weapon when attack hits something.
/// </summary>
public sealed partial class CEAttackUsingEvent(EntityUid user, List<EntityUid> targets) : EntityEventArgs
{
    public EntityUid User = user;
    public List<EntityUid> Targets = targets;
}

/// <summary>
/// Raised on attacked entity when it gets hit by a CEMeleeWeaponComponent attack.
/// </summary>
public sealed partial class CEAttackedEvent(EntityUid attacker, EntityUid weapon)
{
    public EntityUid Attacker = attacker;
    public EntityUid Weapon = weapon;
}

/// <summary>
/// Raised on attacker, after it attacks something with a CEMeleeWeaponComponent
/// </summary>
public sealed partial class CEAfterAttackEvent(EntityUid weapon, List<EntityUid> targets)
{
    public EntityUid Weapon = weapon;
    public List<EntityUid> Targets = targets;
}

/// <summary>
/// Raised on the server and sent to clients to play melee attack visual effects.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEMeleeAttackEffectEvent(NetEntity user, List<NetEntity> targets) : EntityEventArgs
{
    /// <summary>
    /// The user who performed the attack.
    /// </summary>
    public NetEntity User = user;

    /// <summary>
    /// List of entities that were hit by the attack.
    /// </summary>
    public List<NetEntity> Targets = targets;
}
