using Content.Editor.Prototype;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI;

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
        Action? onReset = null)
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

        // Field label
        var label = new Label
        {
            Text = key,
            MinSize = new Vector2(170, 0),
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

        row.AddChild(label);

        // Control area
        if (value != null)
        {
            var control = CreateControl(value, serializationManager, onChanged);
            control.HorizontalExpand = true;

            // Dim inherited/default controls
            if (source is FieldSource.Inherited or FieldSource.Default)
                control.Modulate = new Color(0.6f, 0.6f, 0.7f, 1f);

            row.AddChild(control);
        }
        else
        {
            // No value — show placeholder
            var placeholder = new Label
            {
                Text = "(default)",
                FontColorOverride = EditorMainControl.TextMuted,
                Margin = new Thickness(0, 4, 0, 0),
            };
            row.AddChild(placeholder);
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
    public static Control CreateFieldRow(
        string key,
        DataNode value,
        ISerializationManager serializationManager,
        Action<object?> onChanged)
    {
        return CreateFieldRow(key, value, FieldSource.Local, serializationManager, onChanged);
    }
}
