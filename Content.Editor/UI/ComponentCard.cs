using Content.Editor.Prototype;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI;

/// <summary>
/// A styled collapsible card for a single ECS component.
/// Generic — works for any prototype that has a ComponentRegistry field.
/// Shows field sources (local/inherited) with distinct styling.
/// </summary>
public sealed class ComponentCard : PanelContainer
{
    private readonly string _componentType;
    private readonly MappingDataNode _mapping;
    private readonly MappingDataNode? _inheritedMapping;
    private readonly bool _isLocal;
    private readonly ISerializationManager _serializationManager;
    private readonly BoxContainer _fieldsContainer;
    private bool _isCollapsed = true;

    /// <summary>
    /// Fired when a component field changes. Args: (fieldTag, newValue).
    /// </summary>
    public event Action<string, object?>? OnFieldChanged;

    public ComponentCard(
        string componentType,
        MappingDataNode mapping,
        MappingDataNode? inheritedMapping,
        bool isLocal,
        ISerializationManager serializationManager)
    {
        _componentType = componentType;
        _mapping = mapping;
        _inheritedMapping = inheritedMapping;
        _isLocal = isLocal;
        _serializationManager = serializationManager;

        HorizontalExpand = true;

        // Card styling: local gets solid border + blurple left bar; inherited gets dashed
        if (isLocal)
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = EditorMainControl.BgSurface,
                BorderColor = EditorMainControl.BorderSubtle,
                BorderThickness = new Thickness(3, 1, 1, 1), // 3px left accent
                ContentMarginLeftOverride = 0,
                ContentMarginRightOverride = 0,
                ContentMarginTopOverride = 0,
                ContentMarginBottomOverride = 0,
            };
        }
        else
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = EditorMainControl.BgSurface,
                BorderColor = EditorMainControl.BorderSubtle,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 0,
                ContentMarginRightOverride = 0,
                ContentMarginTopOverride = 0,
                ContentMarginBottomOverride = 0,
            };
        }

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        // Header row
        var headerPanel = new PanelContainer
        {
            HorizontalExpand = true,
        };
        headerPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#ffffff06"),
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(8, 6, 8, 6),
        };

        var collapseBtn = new Button
        {
            Text = ">",
            MinSize = new Vector2(22, 22),
        };

        var typeText = isLocal ? componentType : componentType + " ^";
        var typeLabel = new Label
        {
            Text = typeText,
            HorizontalExpand = true,
            FontColorOverride = isLocal
                ? EditorMainControl.TextPrimary
                : EditorMainControl.TextMuted,
        };

        var removeBtn = new Button
        {
            Text = "x",
            MinSize = new Vector2(22, 22),
            ToolTip = "Remove component",
        };

        header.AddChild(collapseBtn);
        header.AddChild(typeLabel);
        header.AddChild(removeBtn);
        headerPanel.AddChild(header);
        vbox.AddChild(headerPanel);

        // Fields (collapsed by default)
        _fieldsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            Margin = new Thickness(12, 4, 8, 8),
            Visible = false,
        };

        PopulateFields();

        vbox.AddChild(_fieldsContainer);
        AddChild(vbox);

        collapseBtn.OnPressed += _ =>
        {
            _isCollapsed = !_isCollapsed;
            _fieldsContainer.Visible = !_isCollapsed;
            collapseBtn.Text = _isCollapsed ? ">" : "v";
        };
    }

    private void PopulateFields()
    {
        foreach (var (key, value) in _mapping)
        {
            if (key == "type")
                continue;

            // Determine field source within this component
            var source = FieldSource.Local;
            if (!_isLocal)
            {
                source = FieldSource.Inherited;
            }
            else if (_inheritedMapping != null)
            {
                // Field is in local component; check if also in inherited
                source = _mapping.Has(key) ? FieldSource.Local : FieldSource.Inherited;
            }

            var fieldRow = FieldControlFactory.CreateFieldRow(
                key,
                value,
                source,
                _serializationManager,
                newValue => OnFieldChanged?.Invoke(key, newValue));

            _fieldsContainer.AddChild(fieldRow);
        }

        // Show inherited-only fields from parent component
        if (_inheritedMapping != null && _isLocal)
        {
            foreach (var (key, value) in _inheritedMapping)
            {
                if (key == "type")
                    continue;
                if (_mapping.Has(key))
                    continue;

                var fieldRow = FieldControlFactory.CreateFieldRow(
                    key,
                    value,
                    FieldSource.Inherited,
                    _serializationManager,
                    newValue => OnFieldChanged?.Invoke(key, newValue));

                _fieldsContainer.AddChild(fieldRow);
            }
        }
    }
}
