using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Field editor for <see cref="ComponentRegistry"/> fields.
/// Shows a list of components, each with expandable DataField sub-editors.
/// Supports add/remove components, and correctly handles inheritance:
/// the editor stores the LOCAL value (on-disk delta), while displaying
/// the full resolved component list (queried from the prototype manager).
/// </summary>
public sealed class ComponentRegistryFieldEditor : FieldEditorBase
{
    private const int MaxPopupItems = 25;

    private static readonly Color HeaderBg = Color.FromHex("#252545");
    private static readonly Color CompHeaderBg = Color.FromHex("#2a2a4a");
    private static readonly Color CompHeaderHoverBg = Color.FromHex("#343460");
    private static readonly Color PanelBg = Color.FromHex("#1a1a2e");
    private static readonly Color SubRowAlt = Color.FromHex("#1e1e36");
    private static readonly Color BadgeBg = Color.FromHex("#3a3a5c");
    private static readonly Color DeleteBtnColor = Color.FromHex("#CC4444");
    private static readonly Color HintColor = Color.FromHex("#888899");
    private static readonly Color InheritedBg = Color.FromHex("#1a1a28");

    private readonly BoxContainer _root;
    private readonly BoxContainer _compContainer;
    private readonly string? _protoKind;
    private readonly string? _protoId;

    /// <summary>
    /// Local component data parsed from the SetValue string.
    /// Key = component name, Value = mapping string of that component's fields.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> _localComponents = new(StringComparer.Ordinal);

    /// <summary>
    /// Resolved (post-inheritance) component field data.
    /// Key = component name, Value = field name → value.
    /// </summary>
    private Dictionary<string, Dictionary<string, string>> _resolvedComponents = new(StringComparer.Ordinal);

    private bool _suppressEvents;

    public override Control Control => _root;

    public ComponentRegistryFieldEditor(string? protoKind = null, string? protoId = null)
    {
        _protoKind = protoKind;
        _protoId = protoId;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        _compContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        _root.AddChild(_compContainer);

        // Fetch resolved components from prototype manager
        FetchResolvedComponents();
    }

    private void FetchResolvedComponents()
    {
        _resolvedComponents.Clear();

        if (_protoKind == null || _protoId == null)
            return;

        try
        {
            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            if (!protoManager.TryGetKindType(_protoKind, out var protoType))
                return;

            if (!protoManager.TryGetMapping(protoType, _protoId, out var resolvedMapping))
                return;

            if (!resolvedMapping.TryGet<SequenceDataNode>("components", out var resolvedSeq))
                return;

            foreach (var node in resolvedSeq)
            {
                if (node is not MappingDataNode compMapping)
                    continue;

                if (!compMapping.TryGet<ValueDataNode>("type", out var typeNode))
                    continue;

                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (key, value) in compMapping)
                {
                    if (key != "type")
                        fields[key] = PrototypeDataParser.DataNodeToString(value);
                }

                _resolvedComponents[typeNode.Value] = fields;
            }
        }
        catch
        {
            // Prototype not available
        }
    }

    public override string GetValue() => SerializeLocalComponents();

    protected override void SetValueCore(string value)
    {
        _suppressEvents = true;
        _localComponents = ParseComponentRegistryString(value);
        RebuildUI();
        _suppressEvents = false;
    }

    /// <summary>
    /// Optionally receive the full resolved value for display purposes.
    /// </summary>
    public override void SetResolvedValue(string resolvedValue)
    {
        _resolvedComponents = ParseComponentRegistryString(resolvedValue);
        if (!_suppressEvents)
            RebuildUI();
    }

    private void RebuildUI()
    {
        _compContainer.RemoveAllChildren();

        // Merge: show all components from resolved + local (local takes priority)
        var allNames = new HashSet<string>(_resolvedComponents.Keys, StringComparer.Ordinal);
        allNames.UnionWith(_localComponents.Keys);

        foreach (var compName in allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var isLocal = _localComponents.ContainsKey(compName);
            var isInherited = _resolvedComponents.ContainsKey(compName);
            var isInheritedOnly = isInherited && !isLocal;

            AddComponentSection(compName, isLocal, isInheritedOnly);
        }

        // Add component button
        AddAddButton();
    }

    private void AddComponentSection(string compName, bool isLocal, bool isInheritedOnly)
    {
        var compPanel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat(isInheritedOnly ? InheritedBg : CompHeaderBg)
            {
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };

        var compVBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 0,
        };

        // Component header
        var compHeader = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        var nameLabel = new Label
        {
            Text = compName,
            HorizontalExpand = true,
            Margin = new Thickness(4, 2, 4, 2),
        };

        if (isInheritedOnly)
            nameLabel.AddStyleClass(StyleClass.LabelWeak);

        compHeader.AddChild(nameLabel);

        // Inherited tag
        if (isInheritedOnly)
        {
            var tag = new Label
            {
                Text = "(inherited)",
                FontColorOverride = HintColor,
                Margin = new Thickness(4, 2, 4, 2),
            };
            compHeader.AddChild(tag);
        }

        // Delete button for locally-added components (not inherited-only)
        if (isLocal)
        {
            var capturedName = compName;
            var deleteBtn = new Label
            {
                Text = "✕",
                MinWidth = 22,
                FontColorOverride = DeleteBtnColor,
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
                MouseFilter = Control.MouseFilterMode.Stop,
                ToolTip = "Remove component",
                Margin = new Thickness(2, 2, 6, 2),
            };

            deleteBtn.OnKeyBindDown += args =>
            {
                if (args.Function == EngineKeyFunctions.UIClick)
                {
                    _localComponents.Remove(capturedName);
                    FireChangedAndRebuild();
                    args.Handle();
                }
            };

            compHeader.AddChild(deleteBtn);
        }

        compVBox.AddChild(compHeader);

        // Get the C# type for field reflection
        var compFieldInfo = GetComponentFieldInfo(compName);

        // Get field values: prefer local, fall back to resolved
        var localFields = _localComponents.GetValueOrDefault(compName);
        var resolvedFields = _resolvedComponents.GetValueOrDefault(compName);

        // Merge field names
        var allFieldNames = new HashSet<string>(StringComparer.Ordinal);
        if (localFields != null)
            allFieldNames.UnionWith(localFields.Keys);
        if (resolvedFields != null)
            allFieldNames.UnionWith(resolvedFields.Keys);
        if (compFieldInfo != null)
            allFieldNames.UnionWith(compFieldInfo.Keys);

        // Remove non-datafield keys
        allFieldNames.Remove("type");

        var sorted = compFieldInfo != null
            ? allFieldNames.OrderBy(f => compFieldInfo.TryGetValue(f, out var info) ? info.Order : int.MaxValue)
            : allFieldNames.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        var alt = false;
        foreach (var fieldName in sorted)
        {
            var row = new PanelContainer { HorizontalExpand = true };
            if (alt)
                row.PanelOverride = new StyleBoxFlat(SubRowAlt);
            alt = !alt;

            var rowBox = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 4,
            };

            var fieldLabel = new Label
            {
                Text = fieldName,
                MinWidth = 130,
                MaxWidth = 130,
                Margin = new Thickness(12, 2, 4, 2),
                ClipText = true,
            };
            fieldLabel.AddStyleClass(StyleClass.LabelWeak);

            Type? fieldType = compFieldInfo?.GetValueOrDefault(fieldName).Type;
            var editor = FieldEditorFactory.Create(fieldType);

            // Set value: local override if exists, else resolved, else empty
            var displayValue = localFields?.GetValueOrDefault(fieldName)
                               ?? resolvedFields?.GetValueOrDefault(fieldName)
                               ?? "";
            editor.SetValue(displayValue);
            editor.Control.HorizontalExpand = true;
            editor.Control.Margin = new Thickness(4, 1, 4, 1);

            if (isInheritedOnly)
            {
                // Inherited-only components: read-only (show value but don't edit)
                // User can still add the component locally to override
            }

            var capturedComp = compName;
            var capturedField = fieldName;
            editor.OnValueChanged += newValue =>
            {
                if (_suppressEvents)
                    return;

                // Ensure local component entry exists
                if (!_localComponents.ContainsKey(capturedComp))
                    _localComponents[capturedComp] = new Dictionary<string, string>(StringComparer.Ordinal);

                _localComponents[capturedComp][capturedField] = newValue;
                FireChanged();
            };

            rowBox.AddChild(fieldLabel);
            rowBox.AddChild(editor.Control);
            row.AddChild(rowBox);
            compVBox.AddChild(row);
        }

        compPanel.AddChild(compVBox);
        _compContainer.AddChild(compPanel);
    }

    private void AddAddButton()
    {
        var addBtn = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(BadgeBg)
            {
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Stop,
            ToolTip = "Add component",
        };

        var label = new Label
        {
            Text = "+ Add Component",
            HorizontalAlignment = Control.HAlignment.Center,
        };
        addBtn.AddChild(label);

        var normalStyle = new StyleBoxFlat(BadgeBg)
        {
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };
        var hoverStyle = new StyleBoxFlat(Color.FromHex("#4a4a6c"))
        {
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };
        addBtn.OnMouseEntered += _ => addBtn.PanelOverride = hoverStyle;
        addBtn.OnMouseExited += _ => addBtn.PanelOverride = normalStyle;

        addBtn.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                OpenAddComponentPopup(addBtn);
                args.Handle();
            }
        };

        _compContainer.AddChild(addBtn);
    }

    private void OpenAddComponentPopup(Control anchor)
    {
        IComponentFactory? compFactory;
        try
        {
            compFactory = IoCManager.Resolve<IComponentFactory>();
        }
        catch
        {
            return;
        }

        // All existing component names (local + resolved)
        var existingNames = new HashSet<string>(_localComponents.Keys, StringComparer.Ordinal);
        existingNames.UnionWith(_resolvedComponents.Keys);

        var allNames = compFactory.GetAllRegistrations()
            .Select(r => r.Name)
            .Where(n => !existingNames.Contains(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var popup = new Popup();

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(PanelBg)
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
            MinWidth = 280,
            SeparationOverride = 2,
        };

        var searchBox = new LineEdit
        {
            PlaceHolder = "Search components...",
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

        void Rebuild(string filter)
        {
            itemsBox.RemoveAllChildren();

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? allNames
                : allNames.Where(n =>
                    n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var shown = filtered.Take(MaxPopupItems);
            foreach (var name in shown)
            {
                var item = new PanelContainer
                {
                    HorizontalExpand = true,
                    MouseFilter = Control.MouseFilterMode.Stop,
                };

                var itemLabel = new Label
                {
                    Text = name,
                    Margin = new Thickness(6, 2, 6, 2),
                    HorizontalExpand = true,
                    ClipText = true,
                };
                item.AddChild(itemLabel);

                item.OnMouseEntered += _ =>
                    item.PanelOverride = new StyleBoxFlat(BadgeBg);
                item.OnMouseExited += _ =>
                    item.PanelOverride = null;

                var capturedName = name;
                item.OnKeyBindDown += args =>
                {
                    if (args.Function == EngineKeyFunctions.UIClick)
                    {
                        // Add component locally
                        _localComponents[capturedName] =
                            new Dictionary<string, string>(StringComparer.Ordinal);
                        FireChangedAndRebuild();
                        popup.Close();
                        args.Handle();
                    }
                };

                itemsBox.AddChild(item);
            }

            var hidden = filtered.Count - Math.Min(filtered.Count, MaxPopupItems);
            hintLabel.Visible = hidden > 0;
            hintLabel.Text = $"{hidden} components hidden — filter better";
        }

        Rebuild("");
        searchBox.OnTextChanged += args => Rebuild(args.Text);

        IoCManager.Resolve<IUserInterfaceManager>().ModalRoot.AddChild(popup);
        popup.Open(UIBox2.FromDimensions(
            anchor.GlobalPosition,
            new Vector2(Math.Max(anchor.Width, 280), 0)));

        searchBox.GrabKeyboardFocus();
    }

    // ── Helpers ──

    private Dictionary<string, (Type Type, bool Required, int Order)>? GetComponentFieldInfo(string compName)
    {
        try
        {
            var compFactory = IoCManager.Resolve<IComponentFactory>();
            var reg = compFactory.GetRegistration(compName);
            var raw = PrototypeDataParser.GetDataFieldInfo(reg.Type);
            return raw.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value.Type, kv.Value.Required, kv.Value.Order),
                StringComparer.Ordinal);
        }
        catch
        {
            return null;
        }
    }

    private void FireChanged()
    {
        if (!_suppressEvents)
            RaiseValueChanged(SerializeLocalComponents());
    }

    private void FireChangedAndRebuild()
    {
        if (_suppressEvents)
            return;

        RaiseValueChanged(SerializeLocalComponents());
        _suppressEvents = true;
        RebuildUI();
        _suppressEvents = false;
    }

    private string SerializeLocalComponents()
    {
        if (_localComponents.Count == 0)
            return "[]";

        var compStrings = new List<string>();
        foreach (var (compName, fields) in _localComponents.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var parts = new List<string> { $"type: {compName}" };
            foreach (var (fieldName, fieldValue) in fields)
            {
                if (!string.IsNullOrEmpty(fieldValue))
                    parts.Add($"{fieldName}: {fieldValue}");
            }

            compStrings.Add($"{{ {string.Join(", ", parts)} }}");
        }

        return $"[{string.Join(", ", compStrings)}]";
    }

    /// <summary>
    /// Parses a ComponentRegistry string representation into per-component field dictionaries.
    /// Input format: <c>[{ type: Transform, anchored: true }, { type: Sprite, ... }]</c>
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>> ParseComponentRegistryString(string value)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var trimmed = value.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed == "[]")
            return result;

        // Parse the sequence of mappings
        var items = DataDefinitionFieldEditor.ParseSequenceString(trimmed);
        foreach (var item in items)
        {
            var fields = DataDefinitionFieldEditor.ParseMappingString(item);
            if (!fields.TryGetValue("type", out var compName))
                continue;

            var compFields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, val) in fields)
            {
                if (key != "type")
                    compFields[key] = val;
            }

            result[compName] = compFields;
        }

        return result;
    }
}
