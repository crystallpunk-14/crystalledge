using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editor for Dictionary types.
/// Renders each key-value pair as two sub-editors with a red delete button,
/// and an "Add" button at the bottom.
/// Value is serialized as a YAML-style mapping: { key1: val1, key2: val2 }
/// </summary>
public sealed class DictionaryFieldEditor : FieldEditorBase
{
    private static readonly Color DeleteBtnColor = Color.FromHex("#CC4444");
    private static readonly Color AddBtnBg = Color.FromHex("#2a2a4a");
    private static readonly Color AddBtnHoverBg = Color.FromHex("#3a3a5c");

    private readonly BoxContainer _root;
    private readonly BoxContainer _itemsContainer;
    private readonly Type? _keyType;
    private readonly Type? _valueType;
    private readonly List<DictItem> _items = new();

    public override Control Control => _root;

    /// <summary>
    /// Creates a dictionary editor with the given key/value types.
    /// </summary>
    public DictionaryFieldEditor(Type? keyType, Type? valueType)
    {
        _keyType = keyType;
        _valueType = valueType;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        _itemsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };
        _root.AddChild(_itemsContainer);

        // Add button with hover highlight
        var normalStyle = new StyleBoxFlat(AddBtnBg)
        {
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
        var hoverStyle = new StyleBoxFlat(AddBtnHoverBg)
        {
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };

        var addBtn = new PanelContainer
        {
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Stop,
            PanelOverride = normalStyle,
        };

        var addLabel = new Label
        {
            Text = "+ Add",
            HorizontalAlignment = Control.HAlignment.Center,
        };
        addBtn.AddChild(addLabel);

        addBtn.OnMouseEntered += _ => addBtn.PanelOverride = hoverStyle;
        addBtn.OnMouseExited += _ => addBtn.PanelOverride = normalStyle;

        addBtn.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                AddItem(
                    FieldEditorFactory.GetDefaultValue(_keyType),
                    FieldEditorFactory.GetDefaultValue(_valueType));
                CommitValue();
                args.Handle();
            }
        };

        _root.AddChild(addBtn);
    }

    public override string GetValue()
    {
        return SerializeItems();
    }

    protected override void SetValueCore(string value)
    {
        _items.Clear();
        _itemsContainer.RemoveAllChildren();

        var pairs = ParseDictionaryString(value);

        foreach (var (k, v) in pairs)
        {
            AddItemSilent(k, v);
        }
    }

    private void AddItem(string key, string value)
    {
        AddItemSilent(key, value);
    }

    private void AddItemSilent(string key, string value)
    {
        var keyEditor = FieldEditorFactory.Create(_keyType);
        keyEditor.SetValue(key);
        keyEditor.Control.HorizontalExpand = true;
        keyEditor.Control.Margin = new Thickness(0, 0, 2, 0);

        var valueEditor = FieldEditorFactory.Create(_valueType);
        valueEditor.SetValue(value);
        valueEditor.Control.HorizontalExpand = true;
        valueEditor.Control.Margin = new Thickness(2, 0, 4, 0);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 0,
        };

        var separator = new Label
        {
            Text = ":",
            MinWidth = 12,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };

        var deleteBtn = new Label
        {
            Text = "✕",
            MinWidth = 22,
            FontColorOverride = DeleteBtnColor,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Stop,
            ToolTip = "Remove entry",
        };

        var item = new DictItem
        {
            KeyEditor = keyEditor,
            ValueEditor = valueEditor,
            Row = row,
        };

        deleteBtn.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                RemoveItem(item);
                CommitValue();
                args.Handle();
            }
        };

        keyEditor.OnValueChanged += _ => CommitValue();
        valueEditor.OnValueChanged += _ => CommitValue();

        row.AddChild(keyEditor.Control);
        row.AddChild(separator);
        row.AddChild(valueEditor.Control);
        row.AddChild(deleteBtn);

        _items.Add(item);
        _itemsContainer.AddChild(row);
    }

    private void RemoveItem(DictItem item)
    {
        _items.Remove(item);
        _itemsContainer.RemoveChild(item.Row);
    }

    private void CommitValue()
    {
        RaiseValueChanged(SerializeItems());
    }

    private string SerializeItems()
    {
        if (_items.Count == 0)
            return "{}";

        var parts = _items.Select(
            i => $"{i.KeyEditor.GetValue()}: {i.ValueEditor.GetValue()}");
        return $"{{ {string.Join(", ", parts)} }}";
    }

    /// <summary>
    /// Parses a YAML-style mapping string into key-value pairs.
    /// Handles format: { key1: val1, key2: val2 }
    /// </summary>
    private static List<(string Key, string Value)> ParseDictionaryString(string value)
    {
        var result = new List<(string, string)>();

        if (string.IsNullOrWhiteSpace(value) || value == "{}")
            return result;

        var trimmed = value.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            trimmed = trimmed[1..^1];

        if (string.IsNullOrWhiteSpace(trimmed))
            return result;

        // Split by comma, respecting nesting
        var entries = SplitRespectingNesting(trimmed, ',');

        foreach (var entry in entries)
        {
            var colonIdx = entry.IndexOf(':');
            if (colonIdx >= 0)
            {
                var k = entry[..colonIdx].Trim();
                var v = entry[(colonIdx + 1)..].Trim();
                result.Add((k, v));
            }
            else
            {
                result.Add((entry.Trim(), ""));
            }
        }

        return result;
    }

    private static List<string> SplitRespectingNesting(string text, char delimiter)
    {
        var result = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();

        foreach (var ch in text)
        {
            if (ch is '[' or '{')
                depth++;
            else if (ch is ']' or '}')
                depth--;

            if (ch == delimiter && depth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            result.Add(last);

        return result;
    }

    private sealed class DictItem
    {
        public IFieldEditor KeyEditor { get; init; } = default!;
        public IFieldEditor ValueEditor { get; init; } = default!;
        public BoxContainer Row { get; init; } = default!;
    }
}
