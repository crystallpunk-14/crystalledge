using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Read-only label editor for field types that do not have a custom editor yet.
/// </summary>
public sealed class DefaultFieldEditor : IFieldEditor
{
    private readonly Label _label;
    private string _value = "";

    public Control Control => _label;

    public event Action<string>? OnValueChanged;

    public DefaultFieldEditor()
    {
        _label = new Label
        {
            HorizontalExpand = true,
            ClipText = true,
        };
    }

    public string GetValue()
    {
        return _value;
    }

    public void SetValue(string value)
    {
        _value = value;
        _label.Text = value;
    }
}
