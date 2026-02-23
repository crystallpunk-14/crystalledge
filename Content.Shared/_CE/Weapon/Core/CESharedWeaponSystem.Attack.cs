using System.Linq;
using System.Numerics;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._CE.Weapon.Core;

public abstract partial class CESharedWeaponSystem
{
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
}
