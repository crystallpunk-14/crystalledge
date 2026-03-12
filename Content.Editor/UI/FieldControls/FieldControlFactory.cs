using Content.Editor.Prototype;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI.FieldControls;

/// <summary>
/// Factory that creates styled UI controls for editing DataNode values.
/// Partial class — each control type category is in a separate file for modularity.
/// </summary>
public static partial class FieldControlFactory
{
    /// <summary>
    /// Creates a styled labeled row with the appropriate editor control.
    /// Visual styling depends on the field source (local/inherited/default).
    /// </summary>
    public static Control CreateFieldRow(
        string key,
        DataNode? value,
        FieldSource source,
        ISerializationManager serializationManager,
        Action<object?> onChanged,
        Action? onReset = null,
        string? errorMessage = null,
        bool isRequired = false,
        Action<object?>? onOverride = null)
    {
        // Outer panel for the entire row
        var rowPanel = new PanelContainer
        {
            HorizontalExpand = true,
        };

        // Bottom border separator
        var borderStyle = new StyleBoxFlat
        {
            BorderColor = EditorMainControl.BorderSubtle,
            BorderThickness = new Thickness(0, 0, 0, 1),
            ContentMarginBottomOverride = 1,
        };

        // Local fields: add 3px accent left bar
        if (source == FieldSource.Local)
        {
            borderStyle.BorderColor = EditorMainControl.Accent;
            borderStyle.BorderThickness = new Thickness(3, 0, 0, 1);
            borderStyle.ContentMarginLeftOverride = 0;

            // Reset: bottom border stays subtle
            borderStyle = new StyleBoxFlat
            {
                BorderColor = EditorMainControl.BorderSubtle,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginBottomOverride = 1,
            };
        }

        rowPanel.PanelOverride = borderStyle;

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 3, 0, 3),
        };

        // Override bar (3px accent) for local fields
        if (source == FieldSource.Local)
        {
            var overrideBar = new PanelContainer
            {
                MinSize = new Vector2(3, 0),
                VerticalExpand = true,
            };
            overrideBar.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = EditorMainControl.Accent,
            };
            row.AddChild(overrideBar);
        }

        // Field label + optional required asterisk
        var labelContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            MinSize = new Vector2(170, 0),
            SeparationOverride = 2,
        };

        var label = new Label
        {
            Text = key,
            HorizontalAlignment = Control.HAlignment.Left,
            Margin = new Thickness(source == FieldSource.Local ? 4 : 8, 4, 0, 0),
        };

        // Style label based on source
        switch (source)
        {
            case FieldSource.Inherited:
                label.FontColorOverride = EditorMainControl.TextMuted;
                label.Text = key + " ^";
                break;
            case FieldSource.Default:
                label.FontColorOverride = EditorMainControl.TextMuted;
                break;
            case FieldSource.Local:
                label.FontColorOverride = EditorMainControl.TextPrimary;
                break;
        }

        labelContainer.AddChild(label);

        if (isRequired)
        {
            var asterisk = new Label
            {
                Text = "*",
                FontColorOverride = EditorMainControl.ErrorColor,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = "Required field",
            };
            labelContainer.AddChild(asterisk);
        }

        row.AddChild(labelContainer);

        // Control area
        if (value != null)
        {
            var control = CreateControl(value, serializationManager, onChanged);
            control.HorizontalExpand = true;

            // Dim inherited/default controls
            if (source is FieldSource.Inherited or FieldSource.Default)
                control.Modulate = new Color(0.6f, 0.6f, 0.7f);

            row.AddChild(control);
        }
        else
        {
            // No value yet — show muted "(default)" text that acts as a clickable button.
            // Clicking it initializes an empty field value in the YAML so the user can override.
            var defaultBtn = new ContainerButton
            {
                HorizontalExpand = true,
                ToolTip = "Click to override this field",
            };

            var defaultLabel = new Label
            {
                Text = "(default)",
                FontColorOverride = EditorMainControl.TextMuted,
            };

            defaultBtn.AddChild(defaultLabel);

            var normalBox = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            var hoverBox = new StyleBoxFlat { BackgroundColor = EditorMainControl.BgSurfaceHover };
            defaultBtn.StyleBoxOverride = normalBox;
            defaultBtn.OnMouseEntered += _ => defaultBtn.StyleBoxOverride = hoverBox;
            defaultBtn.OnMouseExited += _ => defaultBtn.StyleBoxOverride = normalBox;

            defaultBtn.OnPressed += _ =>
            {
                // Initialize with empty string so the card re-renders this field as Local
                var callback = onOverride ?? onChanged;
                callback("");
            };
            row.AddChild(defaultBtn);
        }

        // Reset button for local fields
        if (source == FieldSource.Local && onReset != null)
        {
            var resetBtn = new Button
            {
                Text = "R",
                ToolTip = "Reset to inherited/default value",
                MinSize = new Vector2(24, 22),
                MaxSize = new Vector2(24, 22),
            };
            resetBtn.OnPressed += _ => onReset();
            row.AddChild(resetBtn);
        }

        // Error indicator: red "!" with tooltip
        if (errorMessage != null)
        {
            var errorLabel = new Label
            {
                Text = "!",
                FontColorOverride = EditorMainControl.ErrorColor,
                ToolTip = errorMessage,
                MinSize = new Vector2(16, 0),
                HorizontalAlignment = Control.HAlignment.Center,
            };
            row.AddChild(errorLabel);
        }

        rowPanel.AddChild(row);
        return rowPanel;
    }

    /// <summary>
    /// Dispatches to the appropriate control factory based on the DataNode type.
    /// </summary>
    private static Control CreateControl(
        DataNode value,
        ISerializationManager serializationManager,
        Action<object?> onChanged)
    {
        return value switch
        {
            ValueDataNode valueNode => CreateValueControl(valueNode, onChanged),
            MappingDataNode mappingNode => CreateMappingControl(mappingNode, serializationManager, onChanged),
            SequenceDataNode sequenceNode => CreateSequenceControl(sequenceNode, serializationManager, onChanged),
            _ => CreateFallbackControl(value),
        };
    }

    /// <summary>
    /// Legacy overload without source info — treats all fields as local.
    /// </summary>
    private static Control CreateFieldRow(
        string key,
        DataNode value,
        ISerializationManager serializationManager,
        Action<object?> onChanged)
    {
        return CreateFieldRow(key, value, FieldSource.Local, serializationManager, onChanged);
    }
}
