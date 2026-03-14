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
    /// Must NOT fire <see cref="OnValueChanged"/>.
    /// </summary>
    void SetValue(string value);

    /// <summary>
    /// Fired when the user commits a new value through the UI
    /// (e.g. Enter key, focus lost, toggle).
    /// Argument is the new text value.
    /// </summary>
    event Action<string>? OnValueChanged;
}

/// <summary>
/// Base class for field editors that automatically suppresses
/// <see cref="OnValueChanged"/> during programmatic <see cref="SetValue"/> calls.
/// Subclasses implement <see cref="SetValueCore"/> and call
/// <see cref="RaiseValueChanged"/> from UI event handlers.
/// </summary>
public abstract class FieldEditorBase : IFieldEditor
{
    private bool _isSettingValue;

    public abstract Control Control { get; }

    public event Action<string>? OnValueChanged;

    public abstract string GetValue();

    /// <summary>
    /// Sets the value without firing <see cref="OnValueChanged"/>.
    /// Subclasses should NOT override this — override <see cref="SetValueCore"/> instead.
    /// </summary>
    public void SetValue(string value)
    {
        _isSettingValue = true;
        SetValueCore(value);
        _isSettingValue = false;
    }

    /// <summary>
    /// Update the UI control to display the given value.
    /// Called inside the suppression guard — event handlers that
    /// call <see cref="RaiseValueChanged"/> are automatically silenced.
    /// </summary>
    protected abstract void SetValueCore(string value);

    /// <summary>
    /// Call this from UI event handlers to fire <see cref="OnValueChanged"/>.
    /// Automatically suppressed during <see cref="SetValue"/> calls.
    /// </summary>
    protected void RaiseValueChanged(string value)
    {
        if (!_isSettingValue)
            OnValueChanged?.Invoke(value);
    }
}
