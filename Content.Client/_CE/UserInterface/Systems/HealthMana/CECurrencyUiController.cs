using Content.Client._CE.UserInterface.Systems.HealthMana.Widgets;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._CE.Currency;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.UserInterface.Systems.HealthMana;

[UsedImplicitly]
public sealed partial class CECurrencyUiController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IPlayerManager _player = default!;

    private CECurrencySystem? _currency;
    private CECurrencyUI? _currencyBar;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public void OnStateEntered(GameplayState state)
    {
        _currency = EntityManager.System<CECurrencySystem>();
    }

    public void OnStateExited(GameplayState state)
    {
        _currency = null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_player.LocalEntity is { } player)
            UpdateCoins(player);
    }

    private void OnScreenLoad()
    {
        _currencyBar = GetCurrencyBar();

        if (_currencyBar == null)
            return;

        if (_player.LocalEntity is { } player)
        {
            _currencyBar.Visible = true;
            UpdateCoins(player);
        }
        else
        {
            _currencyBar.Visible = false;
        }
    }

    private void OnScreenUnload()
    {
        if (_currencyBar != null)
            _currencyBar.Visible = false;

        _currencyBar = null;
    }

    private CECurrencyUI? GetCurrencyBar()
    {
        if (UIManager.ActiveScreen is DefaultGameScreen game)
            return game.CurrencyBar;

        if (UIManager.ActiveScreen is SeparatedChatGameScreen separated)
            return separated.CurrencyBar;

        return null;
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        _currencyBar ??= GetCurrencyBar();

        if (_currencyBar != null)
            _currencyBar.Visible = true;

        UpdateCoins(args.Entity);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        if (_currencyBar != null)
            _currencyBar.Visible = false;
    }

    private void UpdateCoins(EntityUid uid)
    {
        if (_currencyBar == null || _currency == null)
            return;

        if (_player.LocalEntity is not { } local || uid != local)
            return;

        _currencyBar.SetCoins(_currency.GetPriceTotal(uid));
    }
}
