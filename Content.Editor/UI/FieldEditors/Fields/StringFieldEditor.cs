using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editable string field editor using a <see cref="LineEdit"/> control.
/// Commits value on Enter or focus lost.
/// </summary>
public sealed class StringFieldEditor : FieldEditorBase
{
    private readonly LineEdit _lineEdit;

    public override Control Control => _lineEdit;

    public StringFieldEditor()
    {
        _lineEdit = new LineEdit
        {
            HorizontalExpand = true,
        };

        _lineEdit.OnTextEntered += args => RaiseValueChanged(args.Text);
        _lineEdit.OnFocusExit += args => RaiseValueChanged(args.Text);
    }

    public override string GetValue()
    {
        return _lineEdit.Text;
    }

    protected override void SetValueCore(string value)
    {
        _lineEdit.SetText(value);
    }
}
