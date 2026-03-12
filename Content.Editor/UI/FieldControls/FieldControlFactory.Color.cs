using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldControls;

/// <summary>
/// Color-specific control: hex text entry with live-preview swatch.
/// </summary>
public static partial class FieldControlFactory
{
    private static Control CreateColorControl(string currentValue, Action<object?> onChanged)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        // Color preview swatch
        var colorRect = new PanelContainer
        {
            MinSize = new Vector2(30, 24),
        };

        var cleanColor = currentValue.Trim('"');
        var parsedColor = Color.TryFromHex(cleanColor);
        if (parsedColor != null)
        {
            colorRect.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = parsedColor.Value,
                BorderColor = EditorMainControl.Border,
                BorderThickness = new Thickness(1),
            };
        }

        container.AddChild(colorRect);

        var lineEdit = new LineEdit
        {
            Text = currentValue,
            HorizontalExpand = true,
            MaxSize = new Vector2(140, 999),
        };

        lineEdit.OnTextEntered += args =>
        {
            onChanged(args.Text);
            UpdateColorSwatch(colorRect, args.Text);
        };

        lineEdit.OnFocusExit += _ =>
        {
            onChanged(lineEdit.Text);
            UpdateColorSwatch(colorRect, lineEdit.Text);
        };

        container.AddChild(lineEdit);
        return container;
    }

    private static void UpdateColorSwatch(PanelContainer swatch, string text)
    {
        var clean = text.Trim('"');
        var newColor = Color.TryFromHex(clean);
        if (newColor != null)
        {
            swatch.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = newColor.Value,
                BorderColor = EditorMainControl.Border,
                BorderThickness = new Thickness(1),
            };
        }
    }
}
