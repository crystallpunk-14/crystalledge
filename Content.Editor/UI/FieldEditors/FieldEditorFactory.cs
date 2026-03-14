using System.Numerics;
using Content.Editor.UI.FieldEditors.Fields;
using Robust.Shared.Maths;
using Color = Robust.Shared.Maths.Color;

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

        // Integer types
        if (underlying == typeof(int)
            || underlying == typeof(short)
            || underlying == typeof(long)
            || underlying == typeof(byte)
            || underlying == typeof(sbyte)
            || underlying == typeof(ushort)
            || underlying == typeof(uint)
            || underlying == typeof(ulong))
            return new IntFieldEditor();

        // Floating-point types
        if (underlying == typeof(float)
            || underlying == typeof(double)
            || underlying == typeof(TimeSpan))
            return new FloatFieldEditor();

        // 2D vectors (float and integer)
        if (underlying == typeof(Vector2))
            return new VectorFieldEditor(2, isInteger: false);

        if (underlying == typeof(Vector2i))
            return new VectorFieldEditor(2, isInteger: true);

        // 3D vectors (float)
        if (underlying == typeof(Vector3))
            return new VectorFieldEditor(3, isInteger: false);

        // Color — hex input with color swatch and picker popup
        if (underlying == typeof(Color))
            return new ColorFieldEditor();

        return new DefaultFieldEditor();
    }
}
