using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed class CEManaUiController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private CEManaUI? _manaBar;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<CEMagicEnergyLevelChangeEvent>(OnManaStateChanged);
    }

    private void OnScreenLoad()
    {
        _manaBar = GetManaBar();

        if (_manaBar == null)
            return;

        if (_player.LocalEntity is { } player)
            UpdateMana(player);
        else
            _manaBar.Visible = false;
    }

    private void OnScreenUnload()
    {
        if (_manaBar != null)
            _manaBar.Visible = false;

        _manaBar = null;
    }

    private CEManaUI? GetManaBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.ManaBar;

        return null;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _manaBar ??= GetManaBar();
        UpdateMana(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_manaBar != null)
            _manaBar.Visible = false;
    }

    private void OnManaStateChanged(CEMagicEnergyLevelChangeEvent ev)
    {
        if (_player.LocalEntity != ev.Target)
            return;

        UpdateMana(ev.Target);
    }

    private void UpdateMana(EntityUid uid, CEMagicEnergyContainerComponent? container = null)
    {
        if (_manaBar == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
        {
            _manaBar.Visible = false;
            return;
        }

        if (container == null && !EntityManager.TryGetComponent(uid, out container))
        {
            _manaBar.Visible = false;
            return;
        }

        var maxEnergy = (float) container.MaxEnergy;

        if (maxEnergy <= 0f)
        {
            _manaBar.Visible = false;
            return;
        }

        var currentEnergy = (float) container.Energy;
        var ratio = Math.Clamp(currentEnergy / maxEnergy, 0f, 1f);

        _manaBar.Visible = true;
        _manaBar.SetMana(ratio, (int) MathF.Round(currentEnergy), (int) MathF.Round(maxEnergy));
    }
}
