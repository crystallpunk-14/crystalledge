using Content.Client.Items;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared._CE.Charges;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._CE.Charges;

public sealed class CEChargesSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<CEChargesComponent>(ent => new CEChargesStatusControl(ent));
    }
}

public sealed class CEChargesStatusControl : Control
{
    private readonly Entity<CEChargesComponent> _parent;
    private readonly RichTextLabel _label;

    private int _lastCurrent = -1;
    private int _lastMax = -1;

    public CEChargesStatusControl(Entity<CEChargesComponent> parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        Update();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_parent.Comp.CurrentCharges == _lastCurrent && _parent.Comp.MaxCharges == _lastMax)
            return;

        Update();
    }

    private void Update()
    {
        _lastCurrent = _parent.Comp.CurrentCharges;
        _lastMax = _parent.Comp.MaxCharges;
        _label.SetMarkup(Loc.GetString("ce-charges-status",
            ("current", _lastCurrent),
            ("max", _lastMax)));
    }
}
