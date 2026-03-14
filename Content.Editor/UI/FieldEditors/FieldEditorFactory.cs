using System.Collections.Generic;
using System.Numerics;
using Content.Editor.UI.FieldEditors.Fields;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
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

        // Dictionary<TKey, TValue>
        if (underlying.IsGenericType && IsDictionaryType(underlying))
        {
            var genArgs = underlying.GetGenericArguments();
            var keyType = genArgs.Length > 0 ? genArgs[0] : null;
            var valType = genArgs.Length > 1 ? genArgs[1] : null;
            return new DictionaryFieldEditor(keyType, valType);
        }

        // Collections: List<T>, HashSet<T>, IList<T>, ICollection<T>, IEnumerable<T>, arrays
        if (TryGetCollectionElementType(underlying, out var elementType))
            return new CollectionFieldEditor(elementType);

        // ProtoId<T> — filterable dropdown of all prototype IDs for the given kind
        if (TryGetProtoIdKind(underlying, out var protoKind))
            return new ProtoIdFieldEditor(protoKind);

        // EntProtoId (non-generic) — filterable dropdown of entity prototypes
        if (underlying == typeof(EntProtoId))
            return new ProtoIdFieldEditor(typeof(EntityPrototype));

        // Enums — dropdown with all enum values
        if (underlying.IsEnum)
            return new EnumFieldEditor(underlying);

        return new DefaultFieldEditor();
    }

    /// <summary>
    /// Checks if the type is a dictionary-like generic type.
    /// </summary>
    private static bool IsDictionaryType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var def = type.GetGenericTypeDefinition();
        return def == typeof(Dictionary<,>)
            || def == typeof(IDictionary<,>)
            || def == typeof(IReadOnlyDictionary<,>)
            || def == typeof(SortedDictionary<,>);
    }

    /// <summary>
    /// Tries to extract the prototype kind type from <see cref="ProtoId{T}"/> or
    /// <see cref="EntProtoId{T}"/>.
    /// For <c>ProtoId&lt;T&gt;</c>, the kind is T itself (an <see cref="IPrototype"/>).
    /// For <c>EntProtoId&lt;T&gt;</c>, the kind is <see cref="EntityPrototype"/>.
    /// </summary>
    private static bool TryGetProtoIdKind(Type type, out Type? protoKind)
    {
        protoKind = null;

        if (!type.IsGenericType)
            return false;

        var def = type.GetGenericTypeDefinition();

        if (def == typeof(ProtoId<>))
        {
            // T is the IPrototype kind
            protoKind = type.GetGenericArguments()[0];
            return true;
        }

        if (def == typeof(EntProtoId<>))
        {
            // EntProtoId<TComp> always targets EntityPrototype
            protoKind = typeof(EntityPrototype);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to extract the element type from a collection type.
    /// Returns true if the type is a recognized collection.
    /// </summary>
    private static bool TryGetCollectionElementType(Type type, out Type? elementType)
    {
        elementType = null;

        // Arrays
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        // Generic collections: List<T>, HashSet<T>, IList<T>, ISet<T>, ICollection<T>, etc.
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();

            if (def == typeof(List<>)
                || def == typeof(HashSet<>)
                || def == typeof(IList<>)
                || def == typeof(ISet<>)
                || def == typeof(ICollection<>)
                || def == typeof(IEnumerable<>)
                || def == typeof(IReadOnlyList<>)
                || def == typeof(IReadOnlyCollection<>)
                || def == typeof(SortedSet<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a sensible default text value for the given type,
    /// used when adding new collection elements.
    /// </summary>
    public static string GetDefaultValue(Type? fieldType)
    {
        if (fieldType == null)
            return "";

        var underlying = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (underlying == typeof(bool))
            return "False";
        if (underlying == typeof(int) || underlying == typeof(short)
            || underlying == typeof(long) || underlying == typeof(byte)
            || underlying == typeof(sbyte) || underlying == typeof(ushort)
            || underlying == typeof(uint) || underlying == typeof(ulong))
            return "0";
        if (underlying == typeof(float) || underlying == typeof(double))
            return "0";
        if (underlying == typeof(Color))
            return "#FFFFFF";
        if (underlying.IsEnum)
        {
            var values = Enum.GetNames(underlying);
            return values.Length > 0 ? values[0] : "";
        }

        return "";
    }
}
