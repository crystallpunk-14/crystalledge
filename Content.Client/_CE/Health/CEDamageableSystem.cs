using Content.Client.Items;
using Content.Client.Stylesheets;
using Content.Shared._CE.Camera;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Effects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CE.Health;

public sealed class CEDamageableSystem : CESharedDamageableSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageableComponent, ComponentHandleState>(OnHandleState);
        Subs.ItemStatus<CEHealthStatusComponent>(ent => new CEHealthStatusControl(ent));
    }

    /// <summary>
    /// Applies the authoritative server state and raises <see cref="CEDamageChangedEvent"/>
    /// for game-logic and visual systems when damage actually changed.
    /// This fires once per state diff — server-only damage (ranged, environmental) arrives here.
    /// </summary>
    private void OnHandleState(EntityUid uid, CEDamageableComponent comp, ref ComponentHandleState args)
    {
        if (args.Current is not CEDamageableComponentState state)
            return;

        var oldDamage = new CEDamageSpecifier(comp.Damage);

        comp.Damage = new CEDamageSpecifier(state.Damage);

        if (oldDamage.Equals(comp.Damage))
            return;

        var ev = new CEDamageChangedEvent(uid, oldDamage, comp.Damage, predicted: false);
        RaiseLocalEvent(uid, ev, true);
    }

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source, bool isCritical)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Local());

        var shakeTranslation = new CEScreenshakeParameters() { Trauma = 0.4f, DecayRate = 3f, Frequency = 0.008f };
        _shake.Screenshake(target, shakeTranslation, null);
    }
}

public sealed class CEClientMobStateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMobStateComponent, AfterAutoHandleStateEvent>(OnMobStateAfterState);
    }

    private void OnMobStateAfterState(EntityUid uid, CEMobStateComponent comp, ref AfterAutoHandleStateEvent args)
    {
        var stateEv = new CEMobStateChangedEvent(uid, comp.CurrentState, comp.CurrentState);
        RaiseLocalEvent(uid, stateEv, true);
    }
}

public sealed class CEHealthStatusControl : Control
{
    private readonly EntityUid _owner;
    private readonly IEntityManager _entMan;
    private readonly RichTextLabel _label;
    private readonly ProgressBar _progress;

    public CEHealthStatusControl(Entity<CEHealthStatusComponent> parent)
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _owner = parent.Owner;
        _progress = new ProgressBar
        {
            MaxValue = 1,
            Value = 0,
        };
        _progress.SetHeight = 8f;
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#c23030"));
        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#010c13"));
        _progress.Margin = new Thickness(0, 4);
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };

        if (!_entMan.HasComponent<CEDamageableComponent>(parent))
            return;

        var boxContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };

        boxContainer.AddChild(_label);
        boxContainer.AddChild(_progress);

        AddChild(boxContainer);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var damageable = _entMan.System<CESharedDamageableSystem>();
        var info = damageable.GetHealthInfo(_owner);

        if (info.MaxHp <= 0)
        {
            _progress.Value = 0;
            _label.Text = "0/0";
            return;
        }

        _progress.Value = info.Ratio;
        _label.Text = $"{info.CurrentHp}/{info.MaxHp}";

        var color = info.Ratio switch
        {
            >= 0.66f => "#3fc488",
            >= 0.33f => "#f2a93a",
            _ => "#c23030",
        };
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex(color));
    }
}
