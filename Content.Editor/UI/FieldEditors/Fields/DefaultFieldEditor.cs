using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Read-only label editor for field types that do not have a custom editor yet.
/// </summary>
public sealed class DefaultFieldEditor : FieldEditorBase
{
    private readonly Label _label;
    private string _value = "";

    public override Control Control => _label;

    public DefaultFieldEditor()
    {
        _label = new Label
        {
            HorizontalExpand = true,
            ClipText = true,
        };
    }

    public override string GetValue()
    {
        return _value;
    }

    protected override void SetValueCore(string value)
    {
        _value = value;
        _label.Text = value;
    }
}
