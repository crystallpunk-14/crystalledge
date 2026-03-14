using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Color = Robust.Shared.Maths.Color;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Field editor for types marked with [DataDefinition] or [DataRecord].
/// Displays an expandable panel with sub-field editors for each DataField member.
/// Value is serialized as a YAML mapping string: <c>{ key1: val1, key2: val2 }</c>.
/// </summary>
public sealed class DataDefinitionFieldEditor : FieldEditorBase
{
    private static readonly Color HeaderBg = Color.FromHex("#252545");
    private static readonly Color HeaderHoverBg = Color.FromHex("#303060");
    private static readonly Color PanelBg = Color.FromHex("#1a1a2e");
    private static readonly Color SubRowAlt = Color.FromHex("#1e1e36");

    private readonly Type _defType;
    private readonly Dictionary<string, (Type Type, bool Required, int Order, InheritanceBehavior Inh)> _fieldInfo;
    private readonly BoxContainer _root;
    private readonly PanelContainer _header;
    private readonly Label _headerLabel;
    private readonly BoxContainer _fieldsBox;
    private readonly Dictionary<string, IFieldEditor> _subEditors = new();
    private bool _expanded;
    private bool _suppressEvents;

    public override Control Control => _root;

    /// <summary>
    /// The InheritanceBehavior enum from the engine; replicated here for brevity.
    /// </summary>
    private enum InheritanceBehavior
    {
        Default,
        Always,
        Never,
    }

    public DataDefinitionFieldEditor(Type defType)
    {
        _defType = defType;

        // Get field definitions via reflection
        var rawFieldInfo = PrototypeDataParser.GetDataFieldInfo(defType);
        _fieldInfo = new Dictionary<string, (Type, bool, int, InheritanceBehavior)>(StringComparer.Ordinal);
        foreach (var (tag, (type, req, ord, inh)) in rawFieldInfo)
        {
            _fieldInfo[tag] = (type, req, ord, (InheritanceBehavior)(int)inh);
        }

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        // Collapsible header
        _header = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(HeaderBg),
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Stop,
        };

        _headerLabel = new Label
        {
            Text = $"▶ {defType.Name} ({_fieldInfo.Count} fields)",
            Margin = new Thickness(6, 2, 6, 2),
            HorizontalExpand = true,
            ClipText = true,
        };
        _headerLabel.AddStyleClass(StyleClass.LabelWeak);

        _header.AddChild(_headerLabel);
        _header.OnMouseEntered += _ => _header.PanelOverride = new StyleBoxFlat(HeaderHoverBg);
        _header.OnMouseExited += _ => _header.PanelOverride = new StyleBoxFlat(HeaderBg);
        _header.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
            {
                ToggleExpand();
                args.Handle();
            }
        };

        // Fields container (hidden initially)
        _fieldsBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Visible = false,
            SeparationOverride = 0,
        };

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(PanelBg)
            {
                ContentMarginLeftOverride = 8,
            },
            HorizontalExpand = true,
        };
        panel.AddChild(_fieldsBox);

        _root.AddChild(_header);
        _root.AddChild(panel);

        BuildSubEditors();
    }

    private void ToggleExpand()
    {
        _expanded = !_expanded;
        _fieldsBox.Visible = _expanded;
        _headerLabel.Text = _expanded
            ? $"▼ {_defType.Name} ({_fieldInfo.Count} fields)"
            : $"▶ {_defType.Name} ({_fieldInfo.Count} fields)";
    }

    private void BuildSubEditors()
    {
        var sorted = _fieldInfo
            .OrderBy(kv => kv.Value.Order)
            .ToList();

        var alt = false;
        foreach (var (tag, (type, _, _, _)) in sorted)
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

            var nameLabel = new Label
            {
                Text = tag,
                MinWidth = 130,
                MaxWidth = 130,
                Margin = new Thickness(4, 2, 4, 2),
                ClipText = true,
            };
            nameLabel.AddStyleClass(StyleClass.LabelWeak);

            var editor = FieldEditorFactory.Create(type);
            editor.Control.HorizontalExpand = true;
            editor.Control.Margin = new Thickness(4, 1, 4, 1);

            var capturedTag = tag;
            editor.OnValueChanged += _ =>
            {
                if (!_suppressEvents)
                {
                    RaiseValueChanged(SerializeToString());
                }
            };

            rowBox.AddChild(nameLabel);
            rowBox.AddChild(editor.Control);
            row.AddChild(rowBox);

            _fieldsBox.AddChild(row);
            _subEditors[tag] = editor;
        }
    }

    public override string GetValue() => SerializeToString();

    protected override void SetValueCore(string value)
    {
        _suppressEvents = true;

        var fields = ParseMappingString(value);
        foreach (var (tag, editor) in _subEditors)
        {
            editor.SetValue(fields.GetValueOrDefault(tag, ""));
        }

        _suppressEvents = false;
    }

    private string SerializeToString()
    {
        var parts = new List<string>();
        foreach (var (tag, editor) in _subEditors)
        {
            var val = editor.GetValue();
            if (!string.IsNullOrEmpty(val))
                parts.Add($"{tag}: {val}");
        }

        return parts.Count > 0 ? $"{{ {string.Join(", ", parts)} }}" : "{}";
    }

    /// <summary>
    /// Parses a mapping string <c>{ key1: val1, key2: val2 }</c> into a dictionary.
    /// </summary>
    internal static Dictionary<string, string> ParseMappingString(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = value.Trim();

        if (trimmed.Length < 2)
            return result;

        // Remove braces
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            trimmed = trimmed[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return result;

        var entries = SplitRespectingNesting(trimmed, ',');
        foreach (var entry in entries)
        {
            var idx = FindFirstColonOutsideNesting(entry);
            if (idx < 0)
                continue;

            var key = entry[..idx].Trim();
            var val = entry[(idx + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                result[key] = val;
        }

        return result;
    }

    /// <summary>
    /// Parses a sequence string <c>[item1, item2, ...]</c> into a list.
    /// </summary>
    internal static List<string> ParseSequenceString(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2)
            return new List<string>();

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1].Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return new List<string>();

        return SplitRespectingNesting(trimmed, ',')
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
    }

    /// <summary>
    /// Splits by delimiter while respecting nested brackets and braces.
    /// </summary>
    internal static List<string> SplitRespectingNesting(string text, char delimiter)
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

    /// <summary>
    /// Finds the first colon that's not inside nested brackets/braces.
    /// </summary>
    private static int FindFirstColonOutsideNesting(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is '[' or '{')
                depth++;
            else if (ch is ']' or '}')
                depth--;
            else if (ch == ':' && depth == 0)
                return i;
        }

        return -1;
    }
}
