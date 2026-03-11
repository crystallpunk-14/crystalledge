using System.Reflection;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Editor.Prototype;

/// <summary>
/// Extracts [DataField] metadata from prototype types via reflection.
/// Caches results per type for performance.
/// </summary>
public static class PrototypeReflector
{
    private static readonly Dictionary<Type, List<FieldMetadata>> Cache = new();

    /// <summary>
    /// Gets the list of all editable DataField members for a prototype type.
    /// Results are cached per type.
    /// </summary>
    public static List<FieldMetadata> GetFields(Type prototypeType)
    {
        if (Cache.TryGetValue(prototypeType, out var cached))
            return cached;

        var fields = ExtractFields(prototypeType);
        Cache[prototypeType] = fields;
        return fields;
    }

    /// <summary>
    /// Checks whether the given prototype type supports inheritance (IInheritingPrototype).
    /// </summary>
    public static bool SupportsInheritance(Type prototypeType)
    {
        return typeof(IInheritingPrototype).IsAssignableFrom(prototypeType);
    }

    /// <summary>
    /// Checks whether the given prototype C# type has a ComponentRegistry field.
    /// </summary>
    public static bool HasComponents(Type prototypeType)
    {
        return GetFields(prototypeType).Exists(f => f.IsComponentRegistry);
    }

    private static List<FieldMetadata> ExtractFields(Type type)
    {
        var result = new List<FieldMetadata>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Try to create a default instance for reading default values
        object? defaultInstance = null;
        try
        {
            defaultInstance = Activator.CreateInstance(type, nonPublic: true);
        }
        catch
        {
            // Some types cannot be default-constructed
        }

        var current = type;
        while (current != null && current != typeof(object))
        {
            ExtractFromType(current, defaultInstance, result, seen);
            current = current.BaseType;
        }

        return result;
    }

    private static void ExtractFromType(
        Type type,
        object? defaultInstance,
        List<FieldMetadata> result,
        HashSet<string> seen)
    {
        const BindingFlags flags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.DeclaredOnly;

        // Process fields
        foreach (var field in type.GetFields(flags))
        {
            var attr = GetDataFieldAttr(field);
            if (attr == null)
                continue;

            var tag = attr.Tag ?? InferTag(field.Name);
            if (!seen.Add(tag))
                continue;

            object? defaultValue = null;
            try
            {
                if (defaultInstance != null)
                    defaultValue = field.GetValue(defaultInstance);
            }
            catch
            {
                // ignore
            }

            var meta = BuildMetadata(tag, field.FieldType, defaultValue, attr);
            result.Add(meta);
        }

        // Process properties
        foreach (var prop in type.GetProperties(flags))
        {
            var attr = GetDataFieldAttr(prop);
            if (attr == null)
                continue;

            var tag = attr.Tag ?? InferTag(prop.Name);
            if (!seen.Add(tag))
                continue;

            object? defaultValue = null;
            try
            {
                if (defaultInstance != null && prop.CanRead)
                    defaultValue = prop.GetValue(defaultInstance);
            }
            catch
            {
                // ignore
            }

            var meta = BuildMetadata(tag, prop.PropertyType, defaultValue, attr);
            if (!prop.CanWrite)
                meta.IsReadOnly = true;

            result.Add(meta);
        }
    }

    /// <summary>
    /// Gets the [DataField] attribute, filtering out special subclasses (Id, Parent, Abstract).
    /// </summary>
    private static DataFieldAttribute? GetDataFieldAttr(MemberInfo member)
    {
        var attr = member.GetCustomAttribute<DataFieldAttribute>(inherit: false);
        if (attr == null)
            return null;

        // Skip special fields handled separately by the editor
        if (attr is IdDataFieldAttribute)
            return null;
        if (attr is ParentDataFieldAttribute)
            return null;
        if (attr is AbstractDataFieldAttribute)
            return null;

        return attr;
    }

    private static FieldMetadata BuildMetadata(
        string tag,
        Type fieldType,
        object? defaultValue,
        DataFieldAttribute attr)
    {
        var kind = ClassifyType(fieldType);
        var isCompRegistry = fieldType.Name == "ComponentRegistry"
                          || (fieldType.BaseType != null && fieldType.BaseType.Name == "ComponentRegistry");

        var meta = new FieldMetadata
        {
            Tag = tag,
            FieldType = fieldType,
            DefaultValue = defaultValue,
            Kind = isCompRegistry ? FieldKind.ComponentList : kind,
            IsReadOnly = attr.ReadOnly,
            IsServerOnly = attr.ServerOnly,
            IsComponentRegistry = isCompRegistry,
        };

        // Extra info for enums
        if (fieldType.IsEnum)
        {
            meta.EnumValues = System.Enum.GetNames(fieldType);
            if (fieldType.GetCustomAttribute<FlagsAttribute>() != null)
                meta.Kind = FieldKind.Flags;
        }

        // Extra info for generic types
        if (fieldType.IsGenericType)
        {
            var genDef = fieldType.GetGenericTypeDefinition();
            var genArgs = fieldType.GetGenericArguments();

            if (genDef == typeof(List<>) || genDef == typeof(HashSet<>))
            {
                meta.ElementType = genArgs[0];
            }
            else if (genDef == typeof(Dictionary<,>))
            {
                meta.KeyType = genArgs[0];
                meta.ElementType = genArgs[1];
            }

            // ProtoId detection
            if (genDef.FullName?.Contains("ProtoId") == true && genArgs.Length > 0)
            {
                meta.ProtoIdType = genArgs[0];
            }
        }

        // Handle nullable
        var underlying = Nullable.GetUnderlyingType(fieldType);
        if (underlying != null && meta.Kind == FieldKind.Unknown)
        {
            meta.Kind = ClassifyType(underlying);
        }

        return meta;
    }

    private static FieldKind ClassifyType(Type type)
    {
        // Unwrap nullable
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            type = underlying;

        if (type == typeof(bool))
            return FieldKind.Boolean;
        if (type == typeof(int) || type == typeof(short) || type == typeof(byte)
            || type == typeof(uint) || type == typeof(ushort) || type == typeof(sbyte)
            || type == typeof(long) || type == typeof(ulong))
            return FieldKind.Integer;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return FieldKind.Float;
        if (type == typeof(string))
            return FieldKind.Text;
        if (type == typeof(Color))
            return FieldKind.Color;
        if (type == typeof(Vector2))
            return FieldKind.Vector2;
        if (type == typeof(Vector3))
            return FieldKind.Vector3;
        if (type == typeof(Vector4))
            return FieldKind.Vector4;
        if (type.IsEnum)
            return type.GetCustomAttribute<FlagsAttribute>() != null ? FieldKind.Flags : FieldKind.Enum;

        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();

            if (genDef == typeof(List<>) || genDef == typeof(HashSet<>))
                return FieldKind.List;
            if (genDef == typeof(Dictionary<,>))
                return FieldKind.Map;

            if (genDef.FullName?.Contains("ProtoId") == true)
                return FieldKind.ProtoId;
        }

        // Check for DataDefinition attribute
        if (type.GetCustomAttribute<DataDefinitionAttribute>() != null)
            return FieldKind.DataDefinition;
        if (type.GetCustomAttribute<DataRecordAttribute>() != null)
            return FieldKind.DataDefinition;

        return FieldKind.Unknown;
    }

    /// <summary>
    /// Infers the YAML tag from a C# member name.
    /// Strips leading underscore, lowercases first character.
    /// </summary>
    private static string InferTag(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            return memberName;

        var name = memberName.TrimStart('_');
        if (string.IsNullOrEmpty(name))
            return memberName;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
