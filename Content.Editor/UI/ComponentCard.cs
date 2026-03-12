using Content.Editor.Prototype;
using Content.Editor.UI.FieldControls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI;

/// <summary>
/// A styled collapsible card for a single ECS component.
/// Resolves the C# component type and shows ALL DataField fields
/// with proper source detection (local/inherited/default).
/// </summary>
public sealed class ComponentCard : PanelContainer
{
    private readonly string _componentType;
    private readonly MappingDataNode _mapping;
    private readonly MappingDataNode? _inheritedMapping;
    private readonly bool _isLocal;
    private readonly ISerializationManager _serializationManager;
    private readonly Type? _componentCsType;
    private readonly BoxContainer _fieldsContainer;
    private bool _isCollapsed = true;

    /// <summary>
    /// Fired when a component field changes. Args: (fieldTag, newValue).
    /// </summary>
    public event Action<string, object?>? OnFieldChanged;

    /// <summary>
    /// Fired when the user requests removal of this component.
    /// </summary>
    public event Action? OnRemoveRequested;

    public ComponentCard(
        string componentType,
        MappingDataNode mapping,
        MappingDataNode? inheritedMapping,
        bool isLocal,
        ISerializationManager serializationManager,
        IComponentFactory? componentFactory = null)
    {
        _componentType = componentType;
        _mapping = mapping;
        _inheritedMapping = inheritedMapping;
        _isLocal = isLocal;
        _serializationManager = serializationManager;

        // Resolve C# type
        if (componentFactory != null
            && componentFactory.TryGetRegistration(componentType, out var reg))
        {
            _componentCsType = reg.Type;
        }

        HorizontalExpand = true;

        // Card styling: local gets accent left bar; inherited gets plain border
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = EditorMainControl.BgSurface,
            BorderColor = EditorMainControl.BorderSubtle,
            BorderThickness = isLocal
                ? new Thickness(3, 1, 1, 1)
                : new Thickness(1),
            ContentMarginLeftOverride = 0,
            ContentMarginRightOverride = 0,
            ContentMarginTopOverride = 0,
            ContentMarginBottomOverride = 0,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        // Header row
        var headerPanel = new PanelContainer
        {
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Stop,
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
            ToolTip = isLocal ? "Remove component" : "Override inherited component",
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

        removeBtn.OnPressed += _ => OnRemoveRequested?.Invoke();

        // Right-click context menu
        headerPanel.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIRightClick)
                return;

            args.Handle();

            var menu = new EditorContextMenu();
            menu.AddItem(_isCollapsed ? "Expand" : "Collapse",
                () =>
            {
                _isCollapsed = !_isCollapsed;
                _fieldsContainer.Visible = !_isCollapsed;
                collapseBtn.Text = _isCollapsed ? ">" : "v";
            });

            menu.AddSeparator();
            menu.AddItem("Remove component", () => OnRemoveRequested?.Invoke(), danger: true);

            UserInterfaceManager.ModalRoot.AddChild(menu);
            menu.OpenAtPosition(args.PointerLocation.Position);
        };
    }

    private void PopulateFields()
    {
        _fieldsContainer.RemoveAllChildren();

        var handledTags = new HashSet<string> { "type" };

        // If we have the C# type, show ALL metadata fields
        if (_componentCsType != null)
        {
            var metaFields = PrototypeReflector.GetFields(_componentCsType);

            foreach (var meta in metaFields)
            {
                handledTags.Add(meta.Tag);

                // Determine source: local has it, inherited has it, or default
                var source = GetComponentFieldSource(meta.Tag);
                var valueNode = GetComponentFieldValue(meta.Tag);

                var fieldRow = FieldControls.FieldControlFactory.CreateFieldRow(
                    meta.Tag,
                    valueNode,
                    source,
                    _serializationManager,
                    newValue => OnFieldChanged?.Invoke(meta.Tag, newValue),
                    source == FieldSource.Local ? () => ResetComponentField(meta.Tag) : null,
                    isRequired: meta.IsRequired,
                    onOverride: newValue => OnFieldOverridden(meta.Tag, newValue));

                _fieldsContainer.AddChild(fieldRow);
            }
        }

        // Show any remaining YAML keys not covered by metadata
        foreach (var (key, value) in _mapping)
        {
            if (handledTags.Contains(key))
                continue;

            var source = GetComponentFieldSource(key);

            var fieldRow = FieldControls.FieldControlFactory.CreateFieldRow(
                key,
                value,
                source,
                _serializationManager,
                newValue => OnFieldChanged?.Invoke(key, newValue));

            _fieldsContainer.AddChild(fieldRow);
        }

        // Show inherited-only keys not in local mapping (only if we DON'T have metadata)
        if (_componentCsType == null && _inheritedMapping != null)
        {
            foreach (var (key, value) in _inheritedMapping)
            {
                if (handledTags.Contains(key) || _mapping.Has(key))
                    continue;

                var fieldRow = FieldControls.FieldControlFactory.CreateFieldRow(
                    key,
                    value,
                    FieldSource.Inherited,
                    _serializationManager,
                    newValue => OnFieldChanged?.Invoke(key, newValue));

                _fieldsContainer.AddChild(fieldRow);
            }
        }
    }

    private FieldSource GetComponentFieldSource(string tag)
    {
        if (_isLocal && _mapping.Has(tag))
            return FieldSource.Local;

        if (!_isLocal)
        {
            // For inherited components, check if the field is in the mapping
            if (_mapping.Has(tag))
                return FieldSource.Inherited;
        }

        if (_inheritedMapping != null && _inheritedMapping.Has(tag))
            return FieldSource.Inherited;

        return FieldSource.Default;
    }

    private DataNode? GetComponentFieldValue(string tag)
    {
        if (_mapping.TryGet(tag, out var localNode))
            return localNode;

        if (_inheritedMapping != null && _inheritedMapping.TryGet(tag, out var inheritedNode))
            return inheritedNode;

        return null;
    }

    private void ResetComponentField(string tag)
    {
        _mapping.Remove(tag);
        OnFieldChanged?.Invoke(tag, null);
        PopulateFields();
    }

    private void OnFieldOverridden(string tag, object? newValue)
    {
        // Add the field to the local mapping
        if (newValue is string strVal)
            _mapping[tag] = new ValueDataNode(strVal);
        else if (newValue != null)
            _mapping[tag] = _serializationManager.WriteValue(newValue.GetType(), newValue);

        OnFieldChanged?.Invoke(tag, newValue);
        PopulateFields();
    }
}
