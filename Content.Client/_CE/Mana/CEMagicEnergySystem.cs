using Content.Client.Items;
using Content.Client.Stylesheets;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._CE.Mana;

public sealed class CEMagicEnergySystem : CESharedMagicEnergySystem
{
    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<CEMagicEnergyExaminableComponent>( ent => new CEMagicEnergyStatusControl(ent));
    }
}

public sealed class CEMagicEnergyStatusControl : Control
{
    private readonly Entity<CEMagicEnergyContainerComponent> _parent;
    private readonly IEntityManager _entMan;
    private readonly RichTextLabel _label;
    private readonly ProgressBar _progress;

    public CEMagicEnergyStatusControl(Entity<CEMagicEnergyExaminableComponent> parent)
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _progress = new ProgressBar
        {
            MaxValue = 1,
            Value = 0
        };
        _progress.SetHeight = 8f;
        _progress.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#3fc488"));
        _progress.BackgroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#0f2d42"));
        _progress.Margin = new Thickness(0, 4);
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };

        if (!_entMan.TryGetComponent<CEMagicEnergyContainerComponent>(parent, out var container))
            return;

        _parent = (parent.Owner, container);

        var boxContainer = new BoxContainer();

        boxContainer.Orientation = BoxContainer.LayoutOrientation.Vertical;

        boxContainer.AddChild(_label);
        boxContainer.AddChild(_progress);

        AddChild(boxContainer);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var maxEnergy = _parent.Comp.MaxEnergy;
        if (maxEnergy <= 0)
        {
            _progress.Value = 0;
            _label.Text = "0%";
            return;
        }
        var energy = _parent.Comp.Energy;
        var ratio = energy / maxEnergy;
        _progress.Value = ratio;
        var power = ratio * 100;
        _label.Text = $"{power}%";
    }
}
