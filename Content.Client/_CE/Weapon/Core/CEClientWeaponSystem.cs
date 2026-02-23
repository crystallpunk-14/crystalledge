using Content.Shared._CE.Weapon.Core;
using Content.Shared._CE.Weapon.Core.Components;
using Content.Shared.Effects;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;

namespace Content.Client._CE.Weapon.Core;

public sealed class CEClientWeaponSystem : CESharedWeaponSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var weaponUid = entityNull.Value;

        if (!TryGetWeapon(weaponUid, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(weaponUid) || !CanAttack(weaponUid, weapon))
        {
            weapon.Value.Comp.Attacking = false;
            return;
        }

        var primaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var secondaryDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        //TODO: AutoAttacking processing via "Attacking" field resets

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
            ClientAttack(weaponUid, coordinates, CEAttackType.Primary);
            return;
        }

        if (secondaryDown == BoundKeyState.Down)
        {
            ClientAttack(weaponUid, coordinates, CEAttackType.Secondary);
            return;
        }
    }

    private void ClientAttack(EntityUid weapon, EntityCoordinates coordinates, CEAttackType attackType)
    {
        RaisePredictiveEvent(new CEWeaponAttackEvent(GetNetCoordinates(coordinates), GetNetEntity(weapon), attackType));
    }
}
