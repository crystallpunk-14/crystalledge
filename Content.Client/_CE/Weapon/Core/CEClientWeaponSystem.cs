using System.Linq;
using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared._CE.Weapon.Core;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.Effects;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Weapon.Core;

public sealed class CEClientWeaponSystem : CESharedWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeNetworkEvent<CEMeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entity = _player.LocalEntity;

        if (entity == null)
            return;

        var user = entity.Value;

        if (!TryGetWeapon(user, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(user) || !CanAttack(user, weapon: weapon))
        {
            weapon.Value.Comp.Attacking = false;
            return;
        }

        var primaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var secondaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        // Release detection — stop attacking when buttons are released.
        if (primaryDown != BoundKeyState.Down && secondaryDown != BoundKeyState.Down)
        {
            if (weapon.Value.Comp.Attacking)
                RaisePredictiveEvent(new CEStopAttackEvent(GetNetEntity(weapon.Value)));

            return;
        }

        if (weapon.Value.Comp.Attacking || weapon.Value.Comp.NextAttack > Timing.CurTime)
            return;

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
            return;

        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        else
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);

        if (primaryDown == BoundKeyState.Down)
        {
            ClientAttack(user, weapon.Value, mousePos, coordinates, CEAttackType.Primary);
            return;
        }

        if (secondaryDown == BoundKeyState.Down)
        {
            ClientAttack(user, weapon.Value, mousePos, coordinates, CEAttackType.Secondary);
        }
    }

    /// <summary>
    /// Resolves the attack prototype for the given button and dispatches
    /// the appropriate network event based on the attack mode.
    /// </summary>
    private void ClientAttack(
        EntityUid user,
        Entity<CEWeaponComponent> weapon,
        MapCoordinates mousePos,
        EntityCoordinates coordinates,
        CEAttackType attackType)
    {
        if (!weapon.Comp.Attacks.TryGetValue(attackType, out var protoId))
            return;

        if (!_proto.TryIndex(protoId, out var action))
            return;

        switch (action.Mode)
        {
            case CEAttackMode.Precise:
                ClientPreciseAttack(user, weapon, mousePos, coordinates, attackType, action);
                break;
            case CEAttackMode.Wide:
                ClientWideAttack(user, weapon, coordinates, attackType, action);
                break;
        }
    }

    /// <summary>
    /// Sends a precise (single-target) attack event.
    /// </summary>
    private void ClientPreciseAttack(
        EntityUid user,
        Entity<CEWeaponComponent> weapon,
        MapCoordinates mousePos,
        EntityCoordinates coordinates,
        CEAttackType attackType,
        CEAttackActionPrototype action)
    {
        var attackerPos = TransformSystem.GetMapCoordinates(user);

        if (mousePos.MapId != attackerPos.MapId ||
            (attackerPos.Position - mousePos.Position).Length() > action.Range)
            return;

        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        // Don't attack if interaction should handle this instead.
        if (Interaction.CombatModeCanHandInteract(user, target))
            return;

        RaisePredictiveEvent(new CEWeaponAttackEvent(
            GetNetCoordinates(coordinates),
            GetNetEntity(weapon),
            attackType,
            target: GetNetEntity(target)));
    }

    /// <summary>
    /// Sends a wide (arc) attack event with the list of hit entities.
    /// </summary>
    private void ClientWideAttack(
        EntityUid user,
        Entity<CEWeaponComponent> weapon,
        EntityCoordinates coordinates,
        CEAttackType attackType,
        CEAttackActionPrototype action)
    {
        if (!_xformQuery.TryGetComponent(user, out var userXform) ||
            !Timing.IsFirstTimePredicted)
            return;

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);

        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = MathF.Min(action.Range, direction.Length());

        var entities = GetNetEntityList(
            ArcRayCast(userPos, direction.ToWorldAngle(), action.Angle, distance, userXform.MapID, user)
                .ToList());

        RaisePredictiveEvent(new CEWeaponAttackEvent(
            GetNetCoordinates(coordinates),
            GetNetEntity(weapon),
            attackType,
            entities: entities.GetRange(0, Math.Min(action.MaxTargets, entities.Count))));
    }

    #region Overrides

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        return Interaction.InRangeUnobstructed(user, target, xform.Coordinates, xform.LocalRotation, range,
            overlapCheck: false);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        _color.RaiseEffect(Color.Red, targets, Filter.Local());
    }

    public override void DoLunge(
        EntityUid user,
        EntityUid weapon,
        Angle angle,
        Vector2 localPos,
        string? animation,
        bool predicted = true)
    {
        // Client plays lunge animations locally via the existing melee effect system.
        // Full animation support (spawning arc entities, etc.) can be added later
        // by subscribing to MeleeLungeEvent patterns or implementing CE-specific effects.
    }

    private void OnMeleeLunge(CEMeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        if (Exists(ent) && Exists(entWeapon))
            DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }

    #endregion
}
