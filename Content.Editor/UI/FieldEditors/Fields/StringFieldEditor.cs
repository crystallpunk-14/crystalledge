using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editable string field editor using a <see cref="LineEdit"/> control.
/// </summary>
public sealed class StringFieldEditor : IFieldEditor
{
    private readonly LineEdit _lineEdit;
    private bool _suppressEvent;

    public Control Control => _lineEdit;

    public event Action<string>? OnValueChanged;

    public StringFieldEditor()
    {
        _lineEdit = new LineEdit
        {
            HorizontalExpand = true,
        };

        _lineEdit.OnTextChanged += OnTextChanged;
    }

    public string GetValue()
    {
        return _lineEdit.Text;
    }

    public void SetValue(string value)
    {
        _suppressEvent = true;
        _lineEdit.SetText(value);
        _suppressEvent = false;
    }

    private void OnTextChanged(LineEdit.LineEditEventArgs args)
    {
        if (!_suppressEvent)
            OnValueChanged?.Invoke(args.Text);
    }
}
