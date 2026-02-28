using Content.Shared._CE.Animation.Item;
using Content.Shared._CE.Animation.Item.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Animation.Item;

public sealed partial class CEClientWeaponSystem : CESharedWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
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

        if (!TryGetWeapon(user, out var used))
            return;

        if (!CombatMode.IsInCombatMode(user) || !CanAttack(user, weapon: used))
        {
            used.Value.Comp.Using = false;
            return;
        }

        var primaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var secondaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        // Release detection — stop attacking when buttons are released.
        if (primaryDown != BoundKeyState.Down && secondaryDown != BoundKeyState.Down)
        {
            if (used.Value.Comp.Using)
                RaisePredictiveEvent(new CEStopWeaponseEvent(GetNetEntity(used.Value)));

            return;
        }

        if (used.Value.Comp.Using)
            return;

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
            return;


        EntityCoordinates coordinates;

        if (MapManager.TryFindGridAt(mousePos, out var gridUid, out _))
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        else
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);

        //Calculate angle from player to target position
        if (!_xformQuery.TryComp(user, out var userXform))
            return;

        var playerPos = TransformSystem.GetMapCoordinates(userXform).Position;
        var targetPos = TransformSystem.ToMapCoordinates(coordinates).Position;
        var direction = targetPos - playerPos;
        var angle = Angle.FromWorldVec(direction);

        if (primaryDown == BoundKeyState.Down)
        {
            ClientUseItem(user, used.Value, angle, CEUseType.Primary);
            return;
        }

        if (secondaryDown == BoundKeyState.Down)
        {
            ClientUseItem(user, used.Value, angle, CEUseType.Secondary);
        }
    }

    private void ClientUseItem(
        EntityUid user,
        Entity<CEWeaponComponent> used,
        Angle angle,
        CEUseType useType)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        RaisePredictiveEvent(new CEWeaponnUseEvent(angle, GetNetEntity(used), useType));
    }
}
