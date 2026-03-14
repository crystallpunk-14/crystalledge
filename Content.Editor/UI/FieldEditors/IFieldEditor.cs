using Robust.Client.UserInterface;

namespace Content.Editor.UI.FieldEditors;

/// <summary>
/// Interface for a field value editor widget.
/// Each field type (string, bool, enum, etc.) has its own implementation.
/// </summary>
public interface IFieldEditor
{
    /// <summary>
    /// The UI control to embed in the field row.
    /// </summary>
    Control Control { get; }

    /// <summary>
    /// Gets the current text representation of the value.
    /// </summary>
    string GetValue();

    /// <summary>
    /// Sets the displayed value from text.
    /// Should NOT fire <see cref="OnValueChanged"/>.
    /// </summary>
    void SetValue(string value);

    /// <summary>
    /// Fired when the user changes the value through the UI.
    /// Argument is the new text value.
    /// </summary>
    event Action<string>? OnValueChanged;
}
