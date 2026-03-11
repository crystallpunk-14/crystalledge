using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;

namespace Content.Editor.UI;

/// <summary>
/// Complex (nested) controls: collapsible mapping blocks, sequence lists, and fallback.
/// </summary>
public static partial class FieldControlFactory
{
    /// <summary>
    /// Collapsible dark block for mapping (object) values.
    /// </summary>
    private static Control CreateMappingControl(
        MappingDataNode mapping,
        ISerializationManager serializationManager,
        Action<object?> onChanged)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var header = new Button
        {
            Text = $"> {{ {mapping.Count} fields }}",
            HorizontalExpand = true,
            TextAlign = Label.AlignMode.Left,
        };

        // Dark surface block for nested content
        var fieldsPanel = new PanelContainer
        {
            Visible = false,
            HorizontalExpand = true,
        };
        fieldsPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = EditorMainControl.BgSurface,
            BorderColor = EditorMainControl.BorderSubtle,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        var fieldsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
        };

        foreach (var (key, value) in mapping)
        {
            var fieldRow = CreateFieldRow(key, value, serializationManager,
                newValue => onChanged(newValue));
            fieldsContainer.AddChild(fieldRow);
        }

        fieldsPanel.AddChild(fieldsContainer);

        header.OnPressed += _ =>
        {
            fieldsPanel.Visible = !fieldsPanel.Visible;
            header.Text = fieldsPanel.Visible
                ? $"v {{ {mapping.Count} fields }}"
                : $"> {{ {mapping.Count} fields }}";
        };

        container.AddChild(header);
        container.AddChild(fieldsPanel);
        return container;
    }

    /// <summary>
    /// Collapsible dark block for sequence (list) values.
    /// </summary>
    private static Control CreateSequenceControl(
        SequenceDataNode sequence,
        ISerializationManager serializationManager,
        Action<object?> onChanged)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var header = new Button
        {
            Text = $"> [ {sequence.Count} items ]",
            HorizontalExpand = true,
            TextAlign = Label.AlignMode.Left,
        };

        var itemsPanel = new PanelContainer
        {
            Visible = false,
            HorizontalExpand = true,
        };
        itemsPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = EditorMainControl.BgSurface,
            BorderColor = EditorMainControl.BorderSubtle,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        var itemsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
        };

        for (var i = 0; i < sequence.Count; i++)
        {
            var itemNode = sequence[i];
            var itemRow = CreateFieldRow($"[{i}]", itemNode, serializationManager,
                newValue => onChanged(newValue));
            itemsContainer.AddChild(itemRow);
        }

        itemsPanel.AddChild(itemsContainer);

        header.OnPressed += _ =>
        {
            itemsPanel.Visible = !itemsPanel.Visible;
            header.Text = itemsPanel.Visible
                ? $"v [ {sequence.Count} items ]"
                : $"> [ {sequence.Count} items ]";
        };

        container.AddChild(header);
        container.AddChild(itemsPanel);
        return container;
    }

    private static Control CreateFallbackControl(DataNode value)
    {
        return new Label
        {
            Text = value.ToString() ?? "(unknown)",
        };
    }
}
