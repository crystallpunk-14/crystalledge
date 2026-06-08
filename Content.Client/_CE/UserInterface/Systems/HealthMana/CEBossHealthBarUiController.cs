using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Boss;
using Content.Shared._CE.Health;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed partial class CEBossHealthBarUiController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IPlayerManager _player = default!;

    private CESharedDamageableSystem? _damageableSystem;
    private CEBossHealthBarUI? _bossBar;

    public override void Initialize()
    {
        base.Initialize();
        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public void OnStateEntered(GameplayState state)
    {
        _damageableSystem = EntityManager.System<CESharedDamageableSystem>();
    }

    public void OnStateExited(GameplayState state)
    {
        _damageableSystem = null;
    }

    private void OnScreenLoad()
    {
        _bossBar = GetBossBar();

        if (_bossBar != null)
            _bossBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_bossBar != null)
            _bossBar.Visible = false;

        _bossBar = null;
    }

    private CEBossHealthBarUI? GetBossBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.BossHealthBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.BossHealthBar;

        return null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_bossBar == null)
            return;

        // Only show bar for bosses on the same map as the local player.
        if (_player.LocalEntity is not { } localPlayer ||
            !EntityManager.TryGetComponent<TransformComponent>(localPlayer, out var playerXform) ||
            playerXform.MapID == MapId.Nullspace)
        {
            _bossBar.Visible = false;
            return;
        }

        var playerMapId = playerXform.MapID;

        var totalCurrentHp = 0f;
        var totalMaxHp = 0f;
        var count = 0;
        EntityUid? singleBoss = null;

        var query = EntityManager.EntityQueryEnumerator<CEBossHealthBarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bar, out var bossXform))
        {
            if (bossXform.MapID != playerMapId)
                continue;

            if (!bar.Active)
                continue;

            var info = _damageableSystem?.GetHealthInfo(uid) ?? default;

            if (info.MaxHp <= 0)
                continue;

            totalCurrentHp += info.CurrentHp;
            totalMaxHp += info.MaxHp;
            count++;

            if (count == 1)
                singleBoss = uid;
            else
                singleBoss = null;
        }

        if (count == 0 || totalMaxHp <= 0f)
        {
            _bossBar.Visible = false;
            return;
        }

        _bossBar.Visible = true;

        var ratio = Math.Clamp(totalCurrentHp / totalMaxHp, 0f, 1f);

        string? bossName = null;
        if (singleBoss.HasValue &&
            EntityManager.TryGetComponent<MetaDataComponent>(singleBoss.Value, out var meta))
        {
            bossName = meta.EntityName;
        }

        _bossBar.SetData(ratio, args.DeltaSeconds, bossName);
    }
}
