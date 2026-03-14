using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editor for collection types (List, HashSet, etc.).
/// Renders each element as a sub-editor row with a red delete button,
/// and an "Add" button at the bottom to append new elements.
/// Value is serialized as a YAML-style sequence: [item1, item2, ...]
/// </summary>
public sealed class CollectionFieldEditor : FieldEditorBase
{
    private static readonly Color DeleteBtnColor = Color.FromHex("#CC4444");
    private static readonly Color AddBtnBg = Color.FromHex("#2a2a4a");
    private static readonly Color AddBtnHoverBg = Color.FromHex("#3a3a5c");

    private readonly BoxContainer _root;
    private readonly BoxContainer _itemsContainer;
    private readonly Type? _elementType;
    private readonly List<CollectionItem> _items = new();

    public override Control Control => _root;

    /// <summary>
    /// Creates a collection editor for the given element type.
    /// </summary>
    /// <param name="elementType">
    /// The C# type of each element in the collection, or null if unknown.
    /// </param>
    public CollectionFieldEditor(Type? elementType)
    {
        _elementType = elementType;

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
                AddItem(FieldEditorFactory.GetDefaultValue(_elementType));
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
        // Clear existing items
        _items.Clear();
        _itemsContainer.RemoveAllChildren();

        // Parse the collection string
        var elements = ParseCollectionString(value);

        foreach (var element in elements)
        {
            AddItemSilent(element);
        }
    }

    private void AddItem(string value)
    {
        AddItemSilent(value);
    }

    private void AddItemSilent(string value)
    {
        var editor = FieldEditorFactory.Create(_elementType);
        editor.SetValue(value);
        editor.Control.HorizontalExpand = true;
        editor.Control.Margin = new Thickness(0, 0, 4, 0);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        // Delete button (red cross)
        var deleteBtn = new Label
        {
            Text = "✕",
            MinWidth = 22,
            FontColorOverride = DeleteBtnColor,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Stop,
            ToolTip = "Remove element",
        };

        var item = new CollectionItem
        {
            Editor = editor,
            Row = row,
            DeleteButton = deleteBtn,
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

        editor.OnValueChanged += _ =>
        {
            CommitValue();
        };

        row.AddChild(editor.Control);
        row.AddChild(deleteBtn);

        _items.Add(item);
        _itemsContainer.AddChild(row);
    }

    private void RemoveItem(CollectionItem item)
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
            return "[]";

        var values = _items.Select(i => i.Editor.GetValue()).ToList();
        return $"[{string.Join(", ", values)}]";
    }

    /// <summary>
    /// Parses a YAML-style collection string into individual element strings.
    /// Handles formats like: [a, b, c] or just comma-separated values.
    /// </summary>
    private static List<string> ParseCollectionString(string value)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(value) || value == "[]")
            return result;

        var trimmed = value.Trim();

        // Strip surrounding brackets if present
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1];

        if (string.IsNullOrWhiteSpace(trimmed))
            return result;

        // Split by comma, but respect nested brackets/braces
        var depth = 0;
        var current = new System.Text.StringBuilder();
        foreach (var ch in trimmed)
        {
            if (ch is '[' or '{')
                depth++;
            else if (ch is ']' or '}')
                depth--;

            if (ch == ',' && depth == 0)
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

    private sealed class CollectionItem
    {
        public IFieldEditor Editor { get; init; } = default!;
        public BoxContainer Row { get; init; } = default!;
        public Label DeleteButton { get; init; } = default!;
    }
}
