using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Field editor for <see cref="ProtoId{T}"/>, <see cref="EntProtoId"/>,
/// and <see cref="EntProtoId{T}"/> fields.
/// Displays a button that opens a filterable popup showing at most 25
/// matching prototype IDs to avoid rendering thousands of controls.
/// </summary>
public sealed class ProtoIdFieldEditor : FieldEditorBase
{
    private const int MaxVisible = 25;

    private static readonly Color PopupBg = Color.FromHex("#1e1e36");
    private static readonly Color ItemHoverBg = Color.FromHex("#3a3a5c");
    private static readonly Color HintColor = Color.FromHex("#888899");

    private readonly List<string> _allIds;
    private readonly Button _button;
    private string _value = "";

    public override Control Control => _button;

    public ProtoIdFieldEditor(Type? protoKind)
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();

        _allIds = new List<string>();

        if (protoKind != null)
        {
            try
            {
                _allIds = protoManager.EnumeratePrototypes(protoKind)
                    .Select(p => p.ID)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                // Prototype kind may not be registered.
            }
        }

        _button = new Button
        {
            Text = "(none)",
            HorizontalExpand = true,
            ClipText = true,
        };

        _button.OnPressed += _ => OpenPopup();
    }

    public override string GetValue() => _value;

    protected override void SetValueCore(string value)
    {
        _value = value;
        _button.Text = string.IsNullOrEmpty(value) ? "(none)" : value;
    }

    private void OpenPopup()
    {
        var popup = new Popup();

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(PopupBg)
            {
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MinWidth = 300,
            SeparationOverride = 2,
        };

        var searchBox = new LineEdit
        {
            PlaceHolder = "Search prototypes...",
            HorizontalExpand = true,
        };

        var scroll = new ScrollContainer
        {
            MaxHeight = 400,
            HorizontalExpand = true,
        };

        var itemsBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 0,
        };

        var hintLabel = new Label
        {
            FontColorOverride = HintColor,
            HorizontalExpand = true,
            HorizontalAlignment = Control.HAlignment.Center,
        };

        scroll.AddChild(itemsBox);

        vbox.AddChild(searchBox);
        vbox.AddChild(scroll);
        vbox.AddChild(hintLabel);

        panel.AddChild(vbox);
        popup.AddChild(panel);

        // Build the filtered list
        void Rebuild(string filter)
        {
            itemsBox.RemoveAllChildren();

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allIds
                : _allIds.Where(id =>
                    id.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var shown = filtered.Take(MaxVisible);
            foreach (var id in shown)
            {
                var item = new PanelContainer
                {
                    HorizontalExpand = true,
                    MouseFilter = Control.MouseFilterMode.Stop,
                };

                var label = new Label
                {
                    Text = id,
                    Margin = new Thickness(6, 2, 6, 2),
                    HorizontalExpand = true,
                    ClipText = true,
                };
                item.AddChild(label);

                item.OnMouseEntered += _ =>
                    item.PanelOverride = new StyleBoxFlat(ItemHoverBg);
                item.OnMouseExited += _ =>
                    item.PanelOverride = null;

                var capturedId = id;
                item.OnKeyBindDown += args =>
                {
                    if (args.Function == EngineKeyFunctions.UIClick)
                    {
                        _value = capturedId;
                        _button.Text = capturedId;
                        RaiseValueChanged(capturedId);
                        popup.Close();
                        args.Handle();
                    }
                };

                itemsBox.AddChild(item);
            }

            var hidden = filtered.Count - Math.Min(filtered.Count, MaxVisible);
            hintLabel.Visible = hidden > 0;
            hintLabel.Text = $"{hidden} prototypes hidden — filter better";
        }

        Rebuild("");

        searchBox.OnTextChanged += args => Rebuild(args.Text);

        IoCManager.Resolve<IUserInterfaceManager>().ModalRoot.AddChild(popup);
        popup.Open(UIBox2.FromDimensions(
            _button.GlobalPosition,
            new Vector2(
                Math.Max(_button.Width, 300),
                0)));

        searchBox.GrabKeyboardFocus();
    }
}
