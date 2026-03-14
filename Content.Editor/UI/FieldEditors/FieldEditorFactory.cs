using Content.Editor.UI.FieldEditors.Fields;

namespace Content.Editor.UI.FieldEditors;

/// <summary>
/// Factory that creates the appropriate <see cref="IFieldEditor"/> based on field type.
/// New field types can be supported by adding cases to <see cref="Create"/>.
/// </summary>
public static class FieldEditorFactory
{
    /// <summary>
    /// Creates a suitable field editor for the given field type.
    /// </summary>
    /// <param name="fieldType">
    /// The C# <see cref="Type"/> of the field, or null if unknown.
    /// </param>
    /// <returns>An appropriate <see cref="IFieldEditor"/> implementation.</returns>
    public static IFieldEditor Create(Type? fieldType)
    {
        if (fieldType == null)
            return new DefaultFieldEditor();

        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (underlying == typeof(string))
            return new StringFieldEditor();

        if (underlying == typeof(bool))
            return new BoolFieldEditor();

        // TODO: Add more field types here:
        // - int/float → numeric LineEdit with validation
        // - enum → OptionButton / dropdown
        // - Color → color picker
        // - ResPath → file picker
        // - List/Sequence → expandable list editor

        return new DefaultFieldEditor();
    }
}
