using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Color field editor with a HEX text input (#RRGGBB) and a colored preview swatch.
/// Clicking the swatch opens a <see cref="ColorSelectorSliders"/> popup for visual selection.
/// </summary>
public sealed class ColorFieldEditor : FieldEditorBase
{
    private readonly BoxContainer _root;
    private readonly LineEdit _hexInput;
    private readonly PanelContainer _swatch;
    private readonly Popup _popup;
    private readonly ColorSelectorSliders _colorSelector;

    private Color _currentColor = Color.White;

    public override Control Control => _root;

    public ColorFieldEditor()
    {
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        _hexInput = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "#FFFFFF",
        };

        _hexInput.OnTextEntered += args => CommitHex(args.Text);
        _hexInput.OnFocusExit += args => CommitHex(args.Text);

        _swatch = new PanelContainer
        {
            MinWidth = 24,
            MaxWidth = 24,
            MinHeight = 24,
            MaxHeight = 24,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Stop,
            PanelOverride = new StyleBoxFlat(Color.White)
            {
                BorderColor = Color.FromHex("#555555"),
                BorderThickness = new Thickness(1),
            },
        };

        _swatch.OnKeyBindDown += args =>
        {
            if (args.Function == Robust.Shared.Input.EngineKeyFunctions.UIClick)
            {
                OpenColorPicker();
                args.Handle();
            }
        };

        // Color picker popup
        _colorSelector = new ColorSelectorSliders
        {
            IsAlphaVisible = false,
            MinWidth = 250,
            MinHeight = 200,
        };

        _colorSelector.OnColorChanged += OnPickerColorChanged;

        _popup = new Popup
        {
            CloseOnClick = true,
            CloseOnEscape = true,
        };
        _popup.AddChild(_colorSelector);

        _root.AddChild(_swatch);
        _root.AddChild(_hexInput);
    }

    public override string GetValue()
    {
        return _currentColor.ToHex();
    }

    protected override void SetValueCore(string value)
    {
        try
        {
            _currentColor = Color.FromHex(value);
        }
        catch
        {
            _currentColor = Color.White;
        }

        _hexInput.SetText(_currentColor.ToHex());
        UpdateSwatch();
    }

    private void CommitHex(string text)
    {
        var hex = text.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;

        try
        {
            _currentColor = Color.FromHex(hex);
            _hexInput.SetText(_currentColor.ToHex());
            UpdateSwatch();
            RaiseValueChanged(_currentColor.ToHex());
        }
        catch
        {
            // Revert to current valid color
            _hexInput.SetText(_currentColor.ToHex());
        }
    }

    private void OnPickerColorChanged(Color color)
    {
        _currentColor = color;
        _hexInput.SetText(_currentColor.ToHex());
        UpdateSwatch();
        RaiseValueChanged(_currentColor.ToHex());
    }

    private void OpenColorPicker()
    {
        _colorSelector.Color = _currentColor;

        // Position popup near the swatch
        var globalPos = _swatch.GlobalPosition;
        var box = UIBox2.FromDimensions(
            globalPos.X,
            globalPos.Y + _swatch.Height + 4,
            260,
            210);

        var uiManager = IoCManager.Resolve<IUserInterfaceManager>();
        uiManager.ModalRoot.AddChild(_popup);
        _popup.Open(box);
        _popup.OnPopupHide += OnPopupClosed;
    }

    private void OnPopupClosed()
    {
        _popup.OnPopupHide -= OnPopupClosed;

        if (_popup.Parent != null)
            _popup.Parent.RemoveChild(_popup);
    }

    private void UpdateSwatch()
    {
        _swatch.PanelOverride = new StyleBoxFlat(_currentColor)
        {
            BorderColor = Color.FromHex("#555555"),
            BorderThickness = new Thickness(1),
        };
    }
}
