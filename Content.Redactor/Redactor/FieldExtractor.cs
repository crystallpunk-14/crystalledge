using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Content.Redactor.Redactor;

/// <summary>
/// Extracts DataField metadata from type members. Handles classification of
/// field types (boolean, integer, enum, ProtoId, list, map, DataDefinition, etc.).
/// </summary>
public sealed class FieldExtractor
{
    private readonly XmlDocReader _xmlDocs;
    private readonly Dictionary<string, DataDefinitionMetadata> _dataDefinitions;

    public FieldExtractor(XmlDocReader xmlDocs, Dictionary<string, DataDefinitionMetadata> dataDefinitions)
    {
        _xmlDocs = xmlDocs;
        _dataDefinitions = dataDefinitions;
    }

    public List<FieldMetadata> ExtractDataFields(Type type)
    {
        var fields = new List<(int order, FieldMetadata meta)>();
        var seen = new HashSet<string>();

        var current = type;
        var baseOrder = 0;
        while (current != null)
        {
            // Get all backing field tokens for proper source-order interleaving
            var fieldDefs = current.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var backingTokens = new Dictionary<string, int>();
            foreach (var f in fieldDefs)
                backingTokens[f.Name] = f.MetadataToken & 0x00FFFFFF;

            // Collect explicit fields (non-backing)
            var members = new List<(int token, MemberInfo member)>();
            foreach (var f in fieldDefs)
            {
                if (f.Name.EndsWith("k__BackingField")) continue;
                members.Add((f.MetadataToken & 0x00FFFFFF, f));
            }

            // Collect properties, using backing field token for ordering
            foreach (var p in current.GetProperties(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var bfName = $"<{p.Name}>k__BackingField";
                var token = backingTokens.GetValueOrDefault(bfName, int.MaxValue);
                members.Add((token, p));
            }

            members.Sort((a, b) => a.token.CompareTo(b.token));

            foreach (var (token, member) in members)
            {
                if (!seen.Add(member.Name))
                    continue;

                var meta = TryBuildFieldMeta(member);
                if (meta != null)
                    fields.Add((baseOrder + token, meta));
            }

            baseOrder += 100_000;
            current = current.BaseType;
        }

        return fields.OrderBy(f => f.order).Select(f => f.meta).ToList();
    }

    private FieldMetadata? TryBuildFieldMeta(MemberInfo member)
    {
        CustomAttributeData? dfAttr = null;
        bool isId = false, isParent = false, isAbstract = false;
        bool alwaysPush = false, neverPush = false;

        foreach (var a in member.CustomAttributes)
        {
            switch (a.AttributeType.Name)
            {
                case "DataFieldAttribute":
                    dfAttr = a;
                    break;
                case "IdDataFieldAttribute":
                    dfAttr = a;
                    isId = true;
                    break;
                case "ParentDataFieldAttribute":
                    dfAttr = a;
                    isParent = true;
                    break;
                case "AbstractDataFieldAttribute":
                    dfAttr = a;
                    isAbstract = true;
                    break;
                case "AlwaysPushInheritanceAttribute":
                    alwaysPush = true;
                    break;
                case "NeverPushInheritanceAttribute":
                    neverPush = true;
                    break;
            }
        }

        if (dfAttr == null)
            return null;

        Type? memberType = member switch
        {
            FieldInfo fi => fi.FieldType,
            PropertyInfo pi => pi.PropertyType,
            _ => null,
        };
        if (memberType == null)
            return null;

        var tag = ResolveTag(dfAttr, member.Name, isId, isParent, isAbstract);
        var required = ResolveRequired(dfAttr);
        var (fieldKind, enumValues, protoTypeArg) = ClassifyType(memberType);

        var meta = new FieldMetadata
        {
            Name = member.Name,
            Tag = tag,
            Type = memberType.Name,
            FullType = memberType.FullName ?? memberType.Name,
            FieldKind = fieldKind,
            Required = required,
            IsId = isId,
            IsParent = isParent,
            IsAbstract = isAbstract,
            AlwaysPushInheritance = alwaysPush ? true : null,
            NeverPushInheritance = neverPush ? true : null,
            ProtoTypeArg = protoTypeArg,
            EnumValues = enumValues,
            Summary = _xmlDocs.GetMemberSummary(member),
        };

        EnrichFieldTypeInfo(meta, memberType);
        return meta;
    }

    private static string ResolveTag(CustomAttributeData attr, string memberName,
        bool isId, bool isParent, bool isAbstract)
    {
        if (isId) return "id";
        if (isParent) return "parent";
        if (isAbstract) return "abstract";

        if (attr.ConstructorArguments.Count > 0 &&
            attr.ConstructorArguments[0].Value is string tag &&
            !string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        return char.ToLowerInvariant(memberName[0]) + memberName[1..];
    }

    private static bool ResolveRequired(CustomAttributeData attr)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (named.MemberName == "Required" && named.TypedValue.Value is bool r)
                return r;
        }

        if (attr.AttributeType.Name == "DataFieldAttribute" &&
            attr.ConstructorArguments.Count >= 4 &&
            attr.ConstructorArguments[3].Value is bool reqArg)
        {
            return reqArg;
        }

        return false;
    }

    public static (string kind, string[]? enumValues, string? protoTypeArg) ClassifyType(Type type)
    {
        var name = type.Name;

        if (name.StartsWith("Nullable") && type.IsGenericType)
        {
            try
            {
                var inner = type.GetGenericArguments();
                if (inner.Length > 0)
                    return ClassifyType(inner[0]);
            }
            catch { /* fall through */ }
        }

        return name switch
        {
            "Boolean" => ("boolean", null, null),
            "String" => ("text", null, null),
            "Int32" or "Int16" or "Int64" or "Byte" or "SByte"
                or "UInt16" or "UInt32" or "UInt64" => ("integer", null, null),
            "Single" or "Double" or "Decimal" => ("float", null, null),
            "EntProtoId" => ("entityProtoId", null, null),
            "Color" => ("color", null, null),
            "SpriteSpecifier" => ("spriteSpecifier", null, null),
            "Vector2" or "Vector2i" => ("vector2", null, null),
            "Vector3" or "Vector3i" => ("vector3", null, null),
            "Vector4" => ("vector4", null, null),
            "Box2" or "Box2i" => ("box2", null, null),
            "TimeSpan" => ("text", null, null),
            "LocId" => ("text", null, null),
            _ when name.StartsWith("ProtoId") && type.IsGenericType => ExtractProtoIdInfo(type),
            _ when name.StartsWith("EntProtoId") && type.IsGenericType => ("entityProtoId", null, null),
            _ when type.IsArray => ("list", null, null),
            _ when IsDictionaryLike(type) => ("map", null, null),
            _ when IsListLike(type) => ("list", null, null),
            _ when type.IsEnum && type.CustomAttributes.Any(a => a.AttributeType.Name == "FlagsAttribute")
                => ("flags", SafeEnumValues(type), null),
            _ when type.IsEnum => ("enum", SafeEnumValues(type), null),
            _ => ("text", null, null),
        };
    }

    /// <summary>
    /// Known generic type names (Name property, with `1/`2 suffix) treated as ordered/unordered lists.
    /// </summary>
    private static readonly HashSet<string> _listTypeNames = new(StringComparer.Ordinal)
    {
        "List`1", "IList`1", "IReadOnlyList`1",
        "ICollection`1", "IReadOnlyCollection`1", "IEnumerable`1",
        "HashSet`1", "ISet`1", "IReadOnlySet`1", "SortedSet`1",
        "Queue`1", "Stack`1", "LinkedList`1",
        "ImmutableArray`1", "ImmutableList`1", "ImmutableHashSet`1",
        "ImmutableQueue`1", "ImmutableStack`1", "ImmutableSortedSet`1",
        "Collection`1", "ReadOnlyCollection`1", "ObservableCollection`1",
    };

    private static readonly HashSet<string> _dictTypeNames = new(StringComparer.Ordinal)
    {
        "Dictionary`2", "IDictionary`2", "IReadOnlyDictionary`2",
        "SortedDictionary`2", "SortedList`2", "ConcurrentDictionary`2",
        "ImmutableDictionary`2", "ImmutableSortedDictionary`2",
    };

    private static bool IsListLike(Type type)
    {
        if (!type.IsGenericType) return false;
        if (_listTypeNames.Contains(type.Name)) return true;
        // Probe interfaces for IEnumerable<T> / ICollection<T> implementations.
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && _listTypeNames.Contains(iface.Name))
                return true;
        }
        return false;
    }

    private static bool IsDictionaryLike(Type type)
    {
        if (!type.IsGenericType) return false;
        if (_dictTypeNames.Contains(type.Name)) return true;
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && _dictTypeNames.Contains(iface.Name))
                return true;
        }
        return false;
    }

    private void EnrichFieldTypeInfo(FieldMetadata field, Type memberType)
    {
        var name = memberType.Name;

        // Unwrap Nullable
        if (name.StartsWith("Nullable") && memberType.IsGenericType)
        {
            try
            {
                var inner = memberType.GetGenericArguments();
                if (inner.Length > 0) { EnrichFieldTypeInfo(field, inner[0]); return; }
            }
            catch { /* fall through */ }
        }

        // List-like (any generic IEnumerable<T> we recognize)
        if (IsListLike(memberType))
        {
            try
            {
                var elemArg = GetListElementType(memberType);
                if (elemArg != null)
                {
                    var (ek, _, ep) = ClassifyType(elemArg);
                    field.ElementKind = ek;
                    field.ElementFullType = elemArg.FullName ?? elemArg.Name;
                    if (ep != null) field.ElementProtoTypeArg = ep;
                }
            }
            catch { /* ignore */ }
        }

        // Array (T[])
        if (memberType.IsArray)
        {
            try
            {
                var elemType = memberType.GetElementType();
                if (elemType != null)
                {
                    var (ek, _, ep) = ClassifyType(elemType);
                    field.ElementKind = ek;
                    field.ElementFullType = elemType.FullName ?? elemType.Name;
                    if (ep != null) field.ElementProtoTypeArg = ep;
                }
            }
            catch { /* ignore */ }
        }

        // Dictionary-like
        if (IsDictionaryLike(memberType))
        {
            try
            {
                var (keyType, valueType) = GetDictionaryKeyValueTypes(memberType);
                if (keyType != null && valueType != null)
                {
                    var (kk, _, kp) = ClassifyType(keyType);
                    var (vk, _, vp) = ClassifyType(valueType);
                    field.KeyKind = kk;
                    field.KeyFullType = keyType.FullName ?? keyType.Name;
                    field.ValueKind = vk;
                    field.ValueFullType = valueType.FullName ?? valueType.Name;
                    if (kp != null) field.KeyProtoTypeArg = kp;
                    if (vp != null) field.ValueProtoTypeArg = vp;
                }
            }
            catch { /* ignore */ }
        }

        // DataDefinition reference
        var fullName = memberType.FullName ?? memberType.Name;
        if (_dataDefinitions.ContainsKey(fullName))
        {
            field.IsDataDefinition = true;
            field.DataDefinitionType = fullName;
        }
    }

    private static Type? GetListElementType(Type type)
    {
        // Direct generic argument first (List<T>, HashSet<T>, ImmutableArray<T>, ...)
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1) return args[0];
        }
        // Otherwise probe IEnumerable<T> interface
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.Name == "IEnumerable`1")
            {
                var args = iface.GetGenericArguments();
                if (args.Length == 1) return args[0];
            }
        }
        return null;
    }

    private static (Type? key, Type? value) GetDictionaryKeyValueTypes(Type type)
    {
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length >= 2) return (args[0], args[1]);
        }
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType &&
                (iface.Name == "IDictionary`2" || iface.Name == "IReadOnlyDictionary`2"))
            {
                var args = iface.GetGenericArguments();
                if (args.Length == 2) return (args[0], args[1]);
            }
        }
        return (null, null);
    }

    private static (string, string[]?, string?) ExtractProtoIdInfo(Type type)
    {
        try
        {
            var args = type.GetGenericArguments();
            if (args.Length > 0)
            {
                var argName = args[0].Name;
                if (argName.EndsWith("Prototype"))
                    argName = argName[..^"Prototype".Length];
                var yamlType = char.ToLowerInvariant(argName[0]) + argName[1..];
                return ("protoId", null, yamlType);
            }
        }
        catch { /* fallback */ }

        return ("protoId", null, null);
    }

    private static string[]? SafeEnumValues(Type type)
    {
        try
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.Name)
                .ToArray();
        }
        catch
        {
            return null;
        }
    }
}
