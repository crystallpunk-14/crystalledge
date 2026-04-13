using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._CE.Health;

public sealed class CEDamageOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly CESharedDamageableSystem _damageable = default!;

    private CEDamageOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEDamageOverlay();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttach);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<CEDamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CEMobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttach(LocalPlayerAttachedEvent args)
    {
        ClearOverlay();

        if (!HasComp<CEDamageableComponent>(args.Entity))
            return;

        UpdateOverlay(args.Entity);

        if (!_overlayManager.HasOverlay<CEDamageOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
        ClearOverlay();
    }

    private void OnDamageChanged(CEDamageChangedEvent args)
    {
        if (args.Target != _playerManager.LocalEntity)
            return;

        UpdateOverlay(args.Target);
    }

    private void OnMobStateChanged(CEMobStateChangedEvent args)
    {
        if (args.Target != _playerManager.LocalEntity)
            return;

        UpdateOverlay(args.Target);
    }

    private void ClearOverlay()
    {
        _overlay.PainLevel = 0f;
        _overlay.CritLevel = 0f;
        _overlay.InCrit = false;
    }

    private void UpdateOverlay(EntityUid uid)
    {
        var info = _damageable.GetHealthInfo(uid);

        if (info.MaxHp <= 0)
        {
            ClearOverlay();
            return;
        }

        if (info.HasMobState && info.MobState == CEMobState.Critical)
        {
            // In critical state: red overlay stays at max, black overlay narrows vision.
            _overlay.InCrit = true;
            _overlay.PainLevel = 1f;

            // CritLevel: 0 = just entered crit, 1 = about to die.
            // RemainingUntilDeath goes from DestroyThreshold down to 0.
            if (info.DestroyThreshold is > 0 && info.RemainingUntilDeath.HasValue)
            {
                _overlay.CritLevel = 1f - Math.Clamp(
                    (float) info.RemainingUntilDeath.Value / info.DestroyThreshold.Value,
                    0f,
                    1f);
            }
            else
            {
                _overlay.CritLevel = 0.5f;
            }
        }
        else
        {
            // Alive state (or no mob state — just use health ratio).
            _overlay.InCrit = false;
            _overlay.CritLevel = 0f;

            // Pain starts at 50% health. At 50% ratio → level 0, at 0% ratio → level 1.
            // ratio goes from 1 (full health) to 0 (no health).
            if (info.Ratio < 0.5f)
            {
                _overlay.PainLevel = 1f - info.Ratio / 0.5f;
            }
            else
            {
                _overlay.PainLevel = 0f;
            }
        }
    }
}
