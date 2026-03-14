using System.Globalization;
using System.Linq;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editable vector field editor with X:/Y:/Z: labeled text fields.
/// Supports 2D and 3D vectors in comma-separated engine format (e.g. "1.5,2.3").
/// Integer variants use int validation, float variants use float validation.
/// Commits value on Enter or focus lost from any component.
/// </summary>
public sealed class VectorFieldEditor : FieldEditorBase
{
    private readonly BoxContainer _root;
    private readonly LineEdit[] _fields;
    private readonly int _componentCount;
    private readonly bool _isInteger;

    private static readonly string[] Labels2D = { "X:", "Y:" };
    private static readonly string[] Labels3D = { "X:", "Y:", "Z:" };

    public override Control Control => _root;

    /// <summary>
    /// Creates a vector field editor.
    /// </summary>
    /// <param name="componentCount">Number of components (2 or 3).</param>
    /// <param name="isInteger">If true, validates as int; otherwise as float.</param>
    public VectorFieldEditor(int componentCount, bool isInteger)
    {
        _componentCount = componentCount;
        _isInteger = isInteger;

        var labels = componentCount == 3 ? Labels3D : Labels2D;
        _fields = new LineEdit[componentCount];

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 2,
        };

        for (var i = 0; i < componentCount; i++)
        {
            var label = new Label
            {
                Text = labels[i],
                Margin = new Thickness(i == 0 ? 0 : 4, 0, 2, 0),
            };
            label.AddStyleClass(StyleClass.LabelWeak);
            _root.AddChild(label);

            var lineEdit = new LineEdit
            {
                HorizontalExpand = true,
                PlaceHolder = "0",
            };

            _fields[i] = lineEdit;

            var capturedIdx = i;
            lineEdit.OnTextChanged += _ => ValidateComponent(capturedIdx);
            lineEdit.OnTextEntered += _ => RaiseValueChanged(GetValue());
            lineEdit.OnFocusExit += _ => RaiseValueChanged(GetValue());

            _root.AddChild(lineEdit);
        }
    }

    public override string GetValue()
    {
        return string.Join(",", _fields.Select(f => f.Text));
    }

    protected override void SetValueCore(string value)
    {
        var parts = value.Split(',');
        for (var i = 0; i < _componentCount; i++)
        {
            var text = i < parts.Length ? parts[i].Trim() : "0";
            _fields[i].SetText(text);
        }
    }

    /// <summary>
    /// Validates the component at the given index, reverting if invalid.
    /// </summary>
    private void ValidateComponent(int index)
    {
        var text = _fields[index].Text;

        if (_isInteger)
        {
            if (!string.IsNullOrEmpty(text) && text != "-" &&
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                RevertLastChar(index);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(text) && text != "-" && text != "." && text != "-.")
            {
                var parseText = text.EndsWith('.') ? text + "0" : text;
                if (!float.TryParse(parseText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    RevertLastChar(index);
                }
            }
        }
    }

    private void RevertLastChar(int index)
    {
        var field = _fields[index];
        var text = field.Text;
        if (text.Length > 0)
        {
            field.SetText(text[..^1]);
        }
    }
}
