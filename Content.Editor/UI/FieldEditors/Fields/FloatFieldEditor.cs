using System.Globalization;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editable float field editor. Validates input on each keystroke,
/// commits value on Enter or focus lost.
/// Uses InvariantCulture (decimal point <c>.</c>) to match the engine.
/// </summary>
public sealed class FloatFieldEditor : FieldEditorBase
{
    private readonly LineEdit _lineEdit;
    private string _lastValidText = "0";

    public override Control Control => _lineEdit;

    public FloatFieldEditor()
    {
        _lineEdit = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = "0",
        };

        _lineEdit.OnTextChanged += OnTextChanged;
        _lineEdit.OnTextEntered += args => RaiseValueChanged(args.Text);
        _lineEdit.OnFocusExit += args => RaiseValueChanged(args.Text);
    }

    public override string GetValue()
    {
        return _lineEdit.Text;
    }

    protected override void SetValueCore(string value)
    {
        _lastValidText = value;
        _lineEdit.SetText(value);
    }

    private void OnTextChanged(LineEdit.LineEditEventArgs args)
    {
        var text = args.Text;

        // Allow empty, sole minus, or trailing dot as intermediate input
        if (string.IsNullOrEmpty(text) || text == "-" || text == "." || text == "-.")
        {
            _lastValidText = text;
            return;
        }

        // Allow trailing dot (user still typing, e.g. "3.")
        var parseText = text.EndsWith('.') ? text + "0" : text;

        if (float.TryParse(parseText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            _lastValidText = text;
        }
        else
        {
            _lineEdit.SetText(_lastValidText);
        }
    }
}
