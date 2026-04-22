using System.Diagnostics.CodeAnalysis;
using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.Health.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.MeleeWeapon;

public abstract partial class CESharedWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly CESharedAnimationActionSystem _animationAction = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeCosts();

        SubscribeAllEvent<CEWeaponUseEvent>(OnClientAttackRequest);
        SubscribeAllEvent<CEStopWeaponUseEvent>(OnClientStopRequest);
        SubscribeAllEvent<CEWeaponArcHitEvent>(OnArcHitEvent);

        SubscribeLocalEvent<CEWieldedWeaponComponent, CEGetWeaponAnimationsEvent>(OnGetWeaponAnimation);
    }

    private void OnClientAttackRequest(CEWeaponUseEvent ev, EntitySessionEventArgs args)
    {
        if (Timing.ApplyingState)
            return;

        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        TryUse(user, weapon.Value, ev.UseType, ev.Angle);
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

    private void OnArcHitEvent(CEWeaponArcHitEvent ev, EntitySessionEventArgs args)
    {
        if (Timing.ApplyingState)
            return;

        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        var targets = GetEntityList(ev.Targets);
        targets = ValidateArcTargets(user, weapon.Value, targets, args.SenderSession);

        TryAttack(user, weapon.Value, targets);
        ApplyArcEffects(user, weapon.Value, targets, ev.EffectSlot);
    }

    /// <summary>
    /// Validates arc attack targets. Server overrides to check range and obstructions.
    /// </summary>
    protected virtual List<EntityUid> ValidateArcTargets(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, ICommonSession? session)
    {
        return targets;
    }

    /// <summary>
    /// Runs nested arc effects on validated targets.
    /// Server overrides to apply damage from the weapon's EffectSlot data.
    /// Client base does nothing — effects are applied in the Effect() loop during prediction.
    /// </summary>
    protected void ApplyArcEffects(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, string? effectSlot)
    {
        if (effectSlot == null
            || !weapon.Comp.EffectSlots.TryGetValue(effectSlot, out var slotEffects)
            || targets.Count == 0)
            return;

        foreach (var target in targets)
        {
            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                user,
                weapon.Owner,
                Angle.Zero,
                1f,
                target,
                null);

            foreach (var slotEffect in slotEffects)
            {
                if (slotEffect is WeaponArcAttack arc)
                {
                    foreach (var childEffect in arc.Effects)
                    {
                        childEffect.Effect(effectArgs);
                    }
                }
            }
        }
    }

    private void OnGetWeaponAnimation(Entity<CEWieldedWeaponComponent> ent, ref CEGetWeaponAnimationsEvent args)
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

    public bool TryUse(
        EntityUid user,
        Entity<CEWeaponComponent> used,
        CEUseType useType,
        Angle angle)
    {
        var curTime = Timing.CurTime;

        if (!Blocker.CanAttack(user))
            return false;

        if (_animationAction.IsPlayingAnimation(user))
            return false;

        //Get animations
        List<CEAnimationEntry> animations = new();

        var animEv = new CEGetWeaponAnimationsEvent(used, useType);
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

        var entry = animations[comboIndex];

        // Check all cost components (stamina, mana, charges, etc.)
        var attemptEv = new CEWeaponUseAttemptEvent(user, useType);
        RaiseLocalEvent(used, attemptEv);
        if (attemptEv.Cancelled)
            return false;

        var animationProtoId = entry.Anim;

        var animationSpeed = GetAnimationSpeed(user, used) * entry.Speed;
        if (!_animationAction.TryPlayAnimationToAngle(user, animationProtoId, angle, used.Owner, animationSpeed))
            return false;

        // Consume resources after animation starts
        var usedEv = new CEWeaponUsedEvent(user, useType);
        RaiseLocalEvent(used, usedEv);

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

        var ev = new CEGetWeaponEvent();
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
    /// Called from <see cref="WeaponArcAttack"/> when arc trace detects targets.
    /// Client overrides to send hit list to server. Server overrides to skip (waits for client event)
    /// unless the attacker is an NPC.
    /// </summary>
    public virtual void HandleArcAttackHit(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets, string? effectSlot)
    {
        TryAttack(user, weapon, targets);
    }

    public bool TryAttack(EntityUid user, Entity<CEWeaponComponent> weapon, List<EntityUid> targets)
    {
        // Only consider entities that can be attacked (have a damageable component).
        var valid = new List<EntityUid>();
        foreach (var target in targets)
        {
            if (!HasComp<CEDamageableComponent>(target))
                continue;

            valid.Add(target);
        }

        if (valid.Count == 0)
            return false;

        foreach (var target in valid)
        {
            var attackedEv = new CEAttackedEvent(user, weapon);
            RaiseLocalEvent(target, attackedEv);
        }

        RaiseAttackEffects(user, valid);
        _audio.PlayPredicted(weapon.Comp.HitSound, weapon, user);

        var usedEv = new CEAttackUsingEvent(user, valid);
        RaiseLocalEvent(weapon, usedEv);

        var attackerEv = new CEAfterAttackEvent(weapon, valid);
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
public sealed partial class CEAttackedEvent(EntityUid attacker, EntityUid weapon) : EntityEventArgs
{
    public EntityUid Attacker = attacker;
    public EntityUid Weapon = weapon;
}

/// <summary>
/// Raised on attacker, after it attacks something with a CEMeleeWeaponComponent
/// </summary>
public sealed partial class CEAfterAttackEvent(EntityUid weapon, List<EntityUid> targets) : EntityEventArgs
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
