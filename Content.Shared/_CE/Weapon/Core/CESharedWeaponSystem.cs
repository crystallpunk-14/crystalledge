using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Weapon.Core;

/// <summary>
/// Base weapon system providing core logic, event handling, and helper methods.
/// Attack flow lives in <c>CESharedWeaponSystem.Attack.cs</c>,
/// sound in <c>CESharedWeaponSystem.Sound.cs</c>.
/// </summary>
public abstract partial class CESharedWeaponSystem : EntitySystem
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

    #region Abstract / Virtual Overrides

    /// <summary>
    /// Plays the lunge and weapon arc animations. Implemented by client and server.
    /// </summary>
    public abstract void DoLunge(
        EntityUid user,
        EntityUid weapon,
        Angle angle,
        Vector2 localPos,
        string? animation,
        bool predicted = true);

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
}
