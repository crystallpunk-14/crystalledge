using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._CE.Charges;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._CE.Charges;

public sealed class CEChargesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<CEChargesComponent>(ent => new CEChargesProgressControl(ent));
    }
}

public sealed class CEChargesProgressControl : Control
{
    private readonly EntityUid _owner;
    private readonly IEntityManager _entMan;
    private readonly RichTextLabel _label;
    private readonly ProgressBar _progress;

    private int _lastCurrent = -1;
    private int _lastMax = -1;

    public CEChargesProgressControl(Entity<CEChargesComponent> parent)
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _owner = parent.Owner;

        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };

        _progress = new ProgressBar
        {
            MaxValue = 1,
            Value = 1,
        };
        _progress.SetWidth = 70f;
        _progress.SetHeight = 10f;
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#3fc488"));
        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#010c13"));
        _progress.Margin = new Thickness(5, 7, 0, 0);

        var boxContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
        };

        boxContainer.AddChild(_label);
        boxContainer.AddChild(_progress);

        AddChild(boxContainer);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entMan.TryGetComponent<CEChargesComponent>(_owner, out var charges))
            return;

        if (charges.CurrentCharges == _lastCurrent && charges.MaxCharges == _lastMax)
            return;

        _lastCurrent = charges.CurrentCharges;
        _lastMax = charges.MaxCharges;

        if (_lastMax <= 0)
        {
            _progress.Value = 0;
            _label.Text = "0/0";
            return;
        }

        var ratio = Math.Clamp((float) _lastCurrent / _lastMax, 0f, 1f);
        _progress.Value = ratio;
        _label.Text = $"{_lastCurrent}/{_lastMax}";

        var color = ratio switch
        {
            >= 0.66f => "#3fc488",
            >= 0.33f => "#f2a93a",
            _ => "#c23030",
        };
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex(color));
    }
}
