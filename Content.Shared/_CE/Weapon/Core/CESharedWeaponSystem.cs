using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Weapon.Core;

public abstract class CESharedWeaponSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] protected readonly MobStateSystem MobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedCombatModeSystem CombatMode = default!;
    [Dependency] protected readonly SharedInteractionSystem Interaction = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

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

    #region Event Handlers

    private void OnClientAttackRequest(CEWeaponAttackEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not {} user)
            return;

        if (!TryGetWeapon(user, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        TryAttack(user, weapon.Value, ev, args.SenderSession);
    }

    private void OnStopAttack(CEStopAttackEvent ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;

        if (user == null)
            return;

        if (!TryGetWeapon(user.Value, out var weapon) ||
            weapon.Value.Owner != GetEntity(ev.Weapon))
            return;

        if (!weapon.Value.Comp.Attacking)
            return;

        weapon.Value.Comp.Attacking = false;
        DirtyField(weapon.Value.Owner, weapon.Value.Comp, nameof(CEWeaponComponent.Attacking));
    }

    private void OnMeleeSelected(Entity<CEWeaponResetOnEquipComponent> ent, ref HandSelectedEvent args)
    {
        if (Paused(ent))
            return;

        if (!_weaponQuery.TryComp(ent, out var weaponComp))
            return;

        weaponComp.NextAttack = Timing.CurTime + ent.Comp.Cooldown;
        DirtyField(ent, weaponComp, nameof(CEWeaponComponent.NextAttack));
    }

    #endregion

    #region Attack Flow

    /// <summary>
    /// Main attack attempt. Validates cooldowns, resolves attack prototype, and dispatches
    /// to <see cref="DoPreciseAttack"/> or <see cref="DoWideAttack"/> based on the action mode.
    /// </summary>
    private bool TryAttack(
        EntityUid user,
        Entity<CEWeaponComponent> weapon,
        CEWeaponAttackEvent attackEvent,
        ICommonSession? session)
    {
        var curTime = Timing.CurTime;

        if (weapon.Comp.NextAttack > curTime)
            return false;

        if (!CombatMode.IsInCombatMode(user))
            return false;

        if (!Blocker.CanAttack(user))
            return false;

        if (!weapon.Comp.Attacks.TryGetValue(attackEvent.AttackType, out var attackProtoId))
            return false;

        if (!_protoManager.TryIndex(attackProtoId, out var action))
            return false;

        var fireRate = TimeSpan.FromSeconds(1f / action.AttackRate);
        var swings = 0;

        if (weapon.Comp.NextAttack < curTime)
            weapon.Comp.NextAttack = curTime;

        while (weapon.Comp.NextAttack <= curTime)
        {
            weapon.Comp.NextAttack += fireRate;
            swings++;
        }

        DirtyField(weapon, weapon.Comp, nameof(CEWeaponComponent.NextAttack));

        // Raise attempt event on the weapon — allows cancelling.
        var ev = new CEWeaponAttackAttemptEvent(user);
        RaiseLocalEvent(weapon, ref ev);

        if (weapon.Comp.SwingBeverage)
        {
            weapon.Comp.SwingLeft = !weapon.Comp.SwingLeft;
            DirtyField(weapon, weapon.Comp, nameof(CEWeaponComponent.SwingLeft));
        }

        if (ev.Cancelled)
        {
            if (ev.Message != null)
                PopupSystem.PopupClient(ev.Message, weapon, user);

            return false;
        }

        // Attack confirmed — execute swings.
        for (var i = 0; i < swings; i++)
        {
            switch (action.Mode)
            {
                case CEAttackMode.Precise:
                    DoPreciseAttack(user, attackEvent, weapon, action, session);
                    break;
                case CEAttackMode.Wide:
                    DoWideAttack(user, attackEvent, weapon, action, session);
                    break;
            }

            DoLungeAnimation(
                user,
                weapon,
                action.Angle,
                TransformSystem.ToMapCoordinates(GetCoordinates(attackEvent.Coordinates)),
                action.AnimationOffset,
                action.Animation);
        }

        var afterAttackEvent = new CEAfterAttackEvent(weapon);
        RaiseLocalEvent(user, ref afterAttackEvent);

        weapon.Comp.Attacking = true;
        DirtyField(weapon, weapon.Comp, nameof(CEWeaponComponent.Attacking));
        return true;
    }

    /// <summary>
    /// Single-target precise attack. Validates target, applies damage, plays sounds.
    /// </summary>
    protected virtual void DoPreciseAttack(
        EntityUid user,
        CEWeaponAttackEvent ev,
        Entity<CEWeaponComponent> weapon,
        CEAttackActionPrototype action,
        ICommonSession? session)
    {
        var damage = GetDamage(weapon, user, action);
        var target = GetEntity(ev.Target);

        // Validate target.
        if (Deleted(target) ||
            !HasComp<DamageableComponent>(target) ||
            !TryComp(target, out TransformComponent? targetXform) ||
            !InRange(user, target.Value, action.Range, session))
        {
            // Miss — still raise event for examination / effects.
            var missEvent = new CEMeleeHitEvent(new List<EntityUid>(), user, weapon, damage, null);
            RaiseLocalEvent(weapon, missEvent);
            PlaySwingSound(user, weapon, action);

            AdminLogger.Add(LogType.MeleeHit, LogImpact.Low,
                $"{ToPrettyString(user):actor} CE attacked (precise) using {ToPrettyString(weapon):tool} and missed");
            return;
        }

        // Can't attack self with own weapon.
        if (weapon.Owner == target)
            return;

        var hitEvent = new CEMeleeHitEvent(new List<EntityUid> { target.Value }, user, weapon, damage, null);
        RaiseLocalEvent(weapon, hitEvent);

        if (hitEvent.Handled)
            return;

        // Contact interactions (forensics DNA, etc.)
        Interaction.DoContactInteraction(user, weapon);
        Interaction.DoContactInteraction(user, target);

        var attackedEvent = new CEAttackedEvent(weapon, user, targetXform.Coordinates);
        RaiseLocalEvent(target.Value, attackedEvent);

        var modifiedDamage = DamageSpecifier.ApplyModifierSets(
            damage + hitEvent.BonusDamage + attackedEvent.BonusDamage,
            hitEvent.ModifiersList);

        var damageResult = Damageable.TryChangeDamage(
            target.Value, modifiedDamage, out var applied, origin: user,
            ignoreResistances: action.ResistanceBypass);

        if (damageResult != null && applied != null)
        {
            AdminLogger.Add(LogType.MeleeHit, LogImpact.Medium,
                $"{ToPrettyString(user):actor} CE attacked (precise) {ToPrettyString(target.Value):subject} using {ToPrettyString(weapon):tool} and dealt {applied.GetTotal():damage} damage");
        }

        PlayHitSound(target.Value, user, GetHighestDamageSound(modifiedDamage), hitEvent.HitSoundOverride, action);

        if (applied?.GetTotal() > FixedPoint2.Zero)
            DoDamageEffect(new List<EntityUid> { target.Value }, user, targetXform);
    }

    /// <summary>
    /// Arc-based wide attack hitting multiple targets.
    /// </summary>
    private bool DoWideAttack(
        EntityUid user,
        CEWeaponAttackEvent ev,
        Entity<CEWeaponComponent> weapon,
        CEAttackActionPrototype action,
        ICommonSession? session)
    {
        if (!TryComp(user, out TransformComponent? userXform))
            return false;

        var targetMap = TransformSystem.ToMapCoordinates(GetCoordinates(ev.Coordinates));

        if (targetMap.MapId != userXform.MapID)
            return false;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = Math.Min(action.Range, direction.Length());

        var damage = GetDamage(weapon, user, action);
        var entities = GetEntityList(ev.Entities);

        if (entities.Count == 0)
        {
            var missEvent = new CEMeleeHitEvent(new List<EntityUid>(), user, weapon, damage, direction);
            RaiseLocalEvent(weapon, missEvent);
            PlaySwingSound(user, weapon, action);

            AdminLogger.Add(LogType.MeleeHit, LogImpact.Low,
                $"{ToPrettyString(user):actor} CE attacked (wide) using {ToPrettyString(weapon):tool} and missed");
            return true;
        }

        // Cap entity count.
        if (entities.Count > action.MaxTargets)
            entities.RemoveRange(action.MaxTargets, entities.Count - action.MaxTargets);

        // Server-side validation of each entity.
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (ArcRaySuccessful(
                    entities[i], userPos, direction.ToWorldAngle(),
                    action.Angle, distance, userXform.MapID, user, session))
            {
                continue;
            }

            entities.RemoveAt(i);
        }

        var targets = new List<EntityUid>();
        var damageQuery = GetEntityQuery<DamageableComponent>();

        foreach (var entity in entities)
        {
            if (entity == user || !damageQuery.HasComponent(entity))
                continue;

            targets.Add(entity);
        }

        var hitEvent = new CEMeleeHitEvent(targets, user, weapon, damage, direction);
        RaiseLocalEvent(weapon, hitEvent);

        if (hitEvent.Handled)
            return true;

        Interaction.DoContactInteraction(user, weapon);

        foreach (var target in targets)
            Interaction.DoContactInteraction(user, target);

        var appliedDamage = new DamageSpecifier();

        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var entity = targets[i];

            // Per-target pacifism / blocker check for untargeted swings.
            if (!Blocker.CanAttack(user, entity))
            {
                targets.RemoveAt(i);
                continue;
            }

            var attackedEvent = new CEAttackedEvent(weapon, user, GetCoordinates(ev.Coordinates));
            RaiseLocalEvent(entity, attackedEvent);

            var modifiedDamage = DamageSpecifier.ApplyModifierSets(
                damage + hitEvent.BonusDamage + attackedEvent.BonusDamage,
                hitEvent.ModifiersList);

            var damageResult = Damageable.ChangeDamage(
                entity, modifiedDamage, origin: user,
                ignoreResistances: action.ResistanceBypass);

            if (damageResult.GetTotal() > FixedPoint2.Zero)
            {
                appliedDamage += damageResult;

                AdminLogger.Add(LogType.MeleeHit, LogImpact.Medium,
                    $"{ToPrettyString(user):actor} CE attacked (wide) {ToPrettyString(entity):subject} using {ToPrettyString(weapon):tool} and dealt {damageResult.GetTotal():damage} damage");
            }
        }

        if (entities.Count != 0)
        {
            var firstTarget = entities.First();
            PlayHitSound(firstTarget, user, GetHighestDamageSound(appliedDamage), hitEvent.HitSoundOverride, action);
        }

        if (appliedDamage.GetTotal() > FixedPoint2.Zero && targets.Count > 0)
            DoDamageEffect(targets, user, Transform(targets[0]));

        return true;
    }

    #endregion

    #region Animation

    private void DoLungeAnimation(
        EntityUid user,
        EntityUid weapon,
        Angle angle,
        MapCoordinates coordinates,
        float animationOffset,
        string? animation)
    {
        if (!TryComp(user, out TransformComponent? userXform))
            return;

        var invMatrix = TransformSystem.GetInvWorldMatrix(userXform);
        var localPos = Vector2.Transform(coordinates.Position, invMatrix);

        if (localPos.LengthSquared() <= 0f)
            return;

        localPos = userXform.LocalRotation.RotateVec(localPos);

        const float bufferLength = 0.2f;
        var visualLength = animationOffset - bufferLength;

        if (localPos.Length() > visualLength)
            localPos = localPos.Normalized() * visualLength;

        DoLunge(user, weapon, angle, localPos, animation);
    }

    public abstract void DoLunge(
        EntityUid user,
        EntityUid weapon,
        Angle angle,
        Vector2 localPos,
        string? animation,
        bool predicted = true);

    #endregion

    #region Abstract / Virtual Overrides

    /// <summary>
    /// Checks whether the target is within range. Server overrides with lag compensation.
    /// </summary>
    protected abstract bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session);

    /// <summary>
    /// Validates a single arc-ray target. Server overrides with lag compensation.
    /// </summary>
    protected virtual bool ArcRaySuccessful(
        EntityUid targetUid,
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore,
        ICommonSession? session)
    {
        return true;
    }

    /// <summary>
    /// Plays a damage flash effect on hit targets. Implemented differently on server and client.
    /// </summary>
    protected abstract void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform);

    #endregion

    #region Helpers

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
        if (!Blocker.CanAttack(user, target))
            return false;

        var ev = new CEAttackAttemptEvent(user, target, weapon);
        RaiseLocalEvent(user, ev);

        return !ev.Cancelled;
    }

    /// <summary>
    /// Creates a copy of the attack action's base damage, applying the universal modifier.
    /// </summary>
    public DamageSpecifier GetDamage(EntityUid weaponUid, EntityUid user, CEAttackActionPrototype action)
    {
        return new DamageSpecifier(action.Damage * Damageable.UniversalMeleeDamageModifier);
    }

    /// <summary>
    /// Casts rays in an arc from the user's position, returning all hit entities.
    /// Used by wide attacks on the client side.
    /// </summary>
    protected HashSet<EntityUid> ArcRayCast(
        Vector2 position,
        Angle angle,
        Angle arcWidth,
        float range,
        MapId mapId,
        EntityUid ignore)
    {
        var widthRad = arcWidth;
        var increments = 1 + 35 * (int) Math.Ceiling(widthRad / (2 * Math.PI));
        var increment = widthRad / increments;
        var baseAngle = angle - widthRad / 2;

        var resSet = new HashSet<EntityUid>();

        for (var i = 0; i < increments; i++)
        {
            var castAngle = new Angle(baseAngle + increment * i);
            var res = _physics.IntersectRay(
                    mapId,
                    new CollisionRay(position, castAngle.ToWorldVec(), AttackMask),
                    range,
                    ignore,
                    false)
                .ToList();

            if (res.Count != 0)
            {
                var resChecked = res.Where(x => x.Distance.Equals(res[0].Distance));
                foreach (var r in resChecked)
                {
                    if (Interaction.InRangeUnobstructed(ignore, r.HitEntity, range + 0.1f, overlapCheck: false))
                        resSet.Add(r.HitEntity);
                }
            }
        }

        return resSet;
    }

    #endregion

    #region Sound

    private void PlaySwingSound(EntityUid user, EntityUid weapon, CEAttackActionPrototype action)
    {
        _audio.PlayPredicted(action.SwingSound, weapon, user);
    }

    private void PlayHitSound(
        EntityUid target,
        EntityUid? user,
        string? damageType,
        SoundSpecifier? hitSoundOverride,
        CEAttackActionPrototype action)
    {
        var playedSound = false;

        if (Deleted(target))
            return;

        var coords = Transform(target).Coordinates;

        // Check target-specific sounds (MeleeSoundComponent on the target).
        if (TryComp<MeleeSoundComponent>(target, out var damageSoundComp))
        {
            if (damageType == null && damageSoundComp.NoDamageSound != null)
            {
                _audio.PlayPredicted(damageSoundComp.NoDamageSound, coords, user,
                    damageSoundComp.NoDamageSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (damageType != null &&
                     damageSoundComp.SoundTypes?.TryGetValue(damageType, out var st) == true)
            {
                _audio.PlayPredicted(st, coords, user, st.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (damageType != null &&
                     damageSoundComp.SoundGroups?.TryGetValue(damageType, out var sg) == true)
            {
                _audio.PlayPredicted(sg, coords, user, sg.Params.WithVariation(0.05f));
                playedSound = true;
            }
        }

        // Use weapon / action sounds.
        if (!playedSound)
        {
            if (hitSoundOverride != null)
            {
                _audio.PlayPredicted(hitSoundOverride, coords, user,
                    hitSoundOverride.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (action.HitSound != null)
            {
                _audio.PlayPredicted(action.HitSound, coords, user,
                    action.HitSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else
            {
                _audio.PlayPredicted(action.NoDamageSound, coords, user,
                    action.NoDamageSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
        }

        // Generic fallbacks.
        if (!playedSound)
        {
            switch (damageType)
            {
                case "Burn":
                case "Heat":
                case "Radiation":
                case "Cold":
                    _audio.PlayPredicted(
                        new SoundPathSpecifier("/Audio/Items/welder.ogg"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
                case null:
                    _audio.PlayPredicted(
                        new SoundCollectionSpecifier("WeakHit"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
                case "Brute":
                    _audio.PlayPredicted(
                        new SoundCollectionSpecifier("MetalThud"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the damage type or group name with the highest damage for sound selection.
    /// </summary>
    public string? GetHighestDamageSound(DamageSpecifier modifiedDamage)
    {
        var groups = modifiedDamage.GetDamagePerGroup(_protoManager);

        if (groups.Count == 1)
            return groups.Keys.First();

        var highestDamage = FixedPoint2.Zero;
        string? highestDamageType = null;

        foreach (var (type, dmg) in modifiedDamage.DamageDict)
        {
            if (dmg <= highestDamage)
                continue;

            highestDamage = dmg;
            highestDamageType = type;
        }

        return highestDamageType;
    }

    #endregion
}
