using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Editable boolean field editor using a <see cref="CheckBox"/> control.
/// Displays a checkbox whose label shows the current bool value.
/// </summary>
public sealed class BoolFieldEditor : IFieldEditor
{
    private readonly CheckBox _checkBox;
    private bool _suppressEvent;

    public Control Control => _checkBox;

    public event Action<string>? OnValueChanged;

    public BoolFieldEditor()
    {
        _checkBox = new CheckBox();
        _checkBox.OnToggled += OnToggled;
    }

    public string GetValue()
    {
        return _checkBox.Pressed ? "True" : "False";
    }

    public void SetValue(string value)
    {
        _suppressEvent = true;
        var isTruthy = value.Equals("True", StringComparison.OrdinalIgnoreCase)
                       || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                       || value == "1";
        _checkBox.Pressed = isTruthy;
        _checkBox.Text = isTruthy ? "True" : "False";
        _suppressEvent = false;
    }

    private void OnToggled(BaseButton.ButtonToggledEventArgs args)
    {
        _checkBox.Text = args.Pressed ? "True" : "False";

        if (!_suppressEvent)
            OnValueChanged?.Invoke(args.Pressed ? "True" : "False");
    }
}
