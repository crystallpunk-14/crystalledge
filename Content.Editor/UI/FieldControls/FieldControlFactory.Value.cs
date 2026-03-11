using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI;

/// <summary>
/// Value-type controls: boolean toggle, integer/float inputs, text entry.
/// </summary>
public static partial class FieldControlFactory
{
    private static Control CreateValueControl(
        Robust.Shared.Serialization.Markdown.Value.ValueDataNode node,
        Action<object?> onChanged)
    {
        var val = node.Value;

        // Boolean detection
        if (bool.TryParse(val, out var boolVal))
            return CreateBoolControl(boolVal, onChanged);

        // Integer detection
        if (int.TryParse(val, out _))
        {
            return CreateTextControl(val, newVal =>
            {
                if (int.TryParse(newVal, out var intResult))
                    onChanged(intResult);
            }, 200);
        }

        // Float detection
        if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return CreateTextControl(val, newVal =>
            {
                if (float.TryParse(newVal, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var floatResult))
                    onChanged(floatResult);
            }, 200);
        }

        // Color detection (hex format)
        if (val.StartsWith('#') || val.StartsWith("\"#"))
            return CreateColorControl(val, onChanged);

        // Default: text entry
        return CreateTextControl(val, newVal => onChanged(newVal));
    }

    private static Control CreateBoolControl(bool currentValue, Action<object?> onChanged)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };

        var checkbox = new CheckBox
        {
            Pressed = currentValue,
        };

        var valueLabel = new Label
        {
            Text = currentValue ? "true" : "false",
        };

        checkbox.OnToggled += args =>
        {
            onChanged(args.Pressed);
            valueLabel.Text = args.Pressed ? "true" : "false";
        };

        container.AddChild(checkbox);
        container.AddChild(valueLabel);
        return container;
    }

    private static Control CreateTextControl(string currentValue, Action<string> onChanged,
        int maxWidth = 0)
    {
        var lineEdit = new LineEdit
        {
            Text = currentValue,
            HorizontalExpand = true,
        };

        if (maxWidth > 0)
            lineEdit.MaxSize = new Vector2(maxWidth, 999);

        lineEdit.OnTextEntered += args => onChanged(args.Text);
        lineEdit.OnFocusExit += _ => onChanged(lineEdit.Text);
        return lineEdit;
    }
}
