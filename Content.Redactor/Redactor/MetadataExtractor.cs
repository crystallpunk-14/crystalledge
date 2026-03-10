using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Content.Redactor.Redactor;

/// <summary>
/// Scans compiled assemblies via MetadataLoadContext to extract
/// all IPrototype types, IComponent types and their [DataField] metadata.
/// Outputs Redactor/metadata.json consumed by the web editor.
/// </summary>
public static class MetadataExtractor
{
    public static void Extract(string solutionRoot)
    {
        var outputDir = Path.Combine(solutionRoot, "Redactor");
        Directory.CreateDirectory(outputDir);

        var serverBinDir = Path.Combine(solutionRoot, "bin", "Content.Server");
        if (!Directory.Exists(serverBinDir))
        {
            Console.Error.WriteLine($"[Redactor] Server bin directory not found: {serverBinDir}");
            Console.Error.WriteLine("[Redactor] Build Content.Server first.");
            return;
        }

        Console.WriteLine("[Redactor] Extracting prototype metadata...");

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeDlls = Directory.GetFiles(runtimeDir, "*.dll");
        var projectDlls = Directory.GetFiles(serverBinDir, "*.dll", SearchOption.TopDirectoryOnly);

        // Build path map: project DLLs take precedence over runtime
        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in runtimeDlls)
            pathMap[Path.GetFileName(p)] = p;
        foreach (var p in projectDlls)
            pathMap[Path.GetFileName(p)] = p;

        var resolver = new PathAssemblyResolver(pathMap.Values);
        using var mlc = new MetadataLoadContext(resolver, "System.Runtime");

        var prototypes = new Dictionary<string, PrototypeMetadata>();
        var components = new Dictionary<string, ComponentMetadata>();

        foreach (var dllPath in projectDlls)
        {
            try
            {
                var assembly = mlc.LoadFromAssemblyPath(dllPath);
                ScanAssembly(assembly, prototypes, components);
            }
            catch
            {
                // Skip assemblies that can't be loaded (native libs, etc.)
            }
        }

        var metadata = new MetadataRoot
        {
            Prototypes = prototypes,
            Components = components,
            DataDefinitions = new Dictionary<string, DataDefinitionMetadata>(_dataDefinitions),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(metadata, options);
        var outputPath = Path.Combine(outputDir, "metadata.json");
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"[Redactor] Extracted {prototypes.Count} prototypes, {components.Count} components");
        Console.WriteLine($"[Redactor] Metadata written to: {outputPath}");
    }

    private static void ScanAssembly(
        Assembly assembly,
        Dictionary<string, PrototypeMetadata> prototypes,
        Dictionary<string, ComponentMetadata> components)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            try
            {
                ScanType(type, prototypes, components);
            }
            catch
            {
                // Skip problematic types silently
            }
        }
    }

    /// <summary>Collected DataDefinition types (type fullName → fields).</summary>
    private static readonly Dictionary<string, DataDefinitionMetadata> _dataDefinitions = new();

    private static void ScanType(
        Type type,
        Dictionary<string, PrototypeMetadata> prototypes,
        Dictionary<string, ComponentMetadata> components)
    {
        // Scan DataDefinition types (standalone serializable classes used as field values)
        var hasDataDef = type.CustomAttributes
            .Any(a => a.AttributeType.Name is "DataDefinitionAttribute"
                or "ImplicitDataDefinitionForInheritorsAttribute");

        if (hasDataDef && !type.IsAbstract)
        {
            var fullName = type.FullName ?? type.Name;
            if (!_dataDefinitions.ContainsKey(fullName))
            {
                var fields = ExtractDataFields(type);
                if (fields.Count > 0)
                {
                    _dataDefinitions[fullName] = new DataDefinitionMetadata
                    {
                        ClassName = fullName,
                        ShortName = type.Name,
                        Fields = fields,
                    };
                }
            }
        }

        var protoAttr = type.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.Name is "PrototypeAttribute" or "PrototypeRecordAttribute");

        if (protoAttr != null)
        {
            var yamlType = InferPrototypeYamlType(protoAttr, type);
            var inheriting = type.GetInterfaces().Any(i => i.Name == "IInheritingPrototype");
            var fields = ExtractDataFields(type);

            prototypes.TryAdd(yamlType, new PrototypeMetadata
            {
                ClassName = type.FullName ?? type.Name,
                YamlType = yamlType,
                Inheriting = inheriting,
                Fields = fields,
            });
        }

        var compAttr = type.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.Name == "RegisterComponentAttribute");

        if (compAttr != null)
        {
            var compName = InferComponentName(type);
            var fields = ExtractDataFields(type);

            components.TryAdd(compName, new ComponentMetadata
            {
                ClassName = type.FullName ?? type.Name,
                Name = compName,
                Fields = fields,
            });
        }
    }

    private static string InferPrototypeYamlType(CustomAttributeData attr, Type type)
    {
        if (attr.ConstructorArguments.Count > 0 &&
            attr.ConstructorArguments[0].Value is string name &&
            !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var typeName = type.Name;
        if (typeName.EndsWith("Prototype"))
            typeName = typeName[..^"Prototype".Length];

        return char.ToLowerInvariant(typeName[0]) + typeName[1..];
    }

    private static string InferComponentName(Type type)
    {
        var name = type.Name;
        if (name.EndsWith("Component"))
            name = name[..^"Component".Length];
        return name;
    }

    private static List<FieldMetadata> ExtractDataFields(Type type)
    {
        var fields = new List<FieldMetadata>();
        var seen = new HashSet<string>();

        var current = type;
        while (current != null)
        {
            foreach (var member in current.GetMembers(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (member is not (FieldInfo or PropertyInfo))
                    continue;
                if (!seen.Add(member.Name))
                    continue;

                var meta = TryBuildFieldMeta(member);
                if (meta != null)
                    fields.Add(meta);
            }

            current = current.BaseType;
        }

        return fields;
    }

    private static FieldMetadata? TryBuildFieldMeta(MemberInfo member)
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
        };

        // Enrich with element/key/value type info for lists, maps, and DataDefinition references
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

    private static (string kind, string[]? enumValues, string? protoTypeArg) ClassifyType(Type type)
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
            "TimeSpan" => ("text", null, null),
            "LocId" => ("text", null, null),
            _ when name.StartsWith("ProtoId") && type.IsGenericType => ExtractProtoIdInfo(type),
            _ when name.StartsWith("EntProtoId") && type.IsGenericType => ("entityProtoId", null, null),
            _ when (name.Contains("List") || name.Contains("HashSet")) && type.IsGenericType
                => ("list", null, null),
            _ when name.Contains("Dictionary") && type.IsGenericType => ("map", null, null),
            _ when type.IsEnum => ("enum", SafeEnumValues(type), null),
            _ when type.IsArray => ("list", null, null),
            _ => ("text", null, null),
        };
    }

    /// <summary>
    /// Extended classification for FieldMetadata with element/key/value types.
    /// </summary>
    private static void EnrichFieldTypeInfo(FieldMetadata field, Type memberType)
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

        // List / HashSet
        if ((name.Contains("List") || name.Contains("HashSet")) && memberType.IsGenericType)
        {
            try
            {
                var args = memberType.GetGenericArguments();
                if (args.Length > 0)
                {
                    var (ek, _, ep) = ClassifyType(args[0]);
                    field.ElementKind = ek;
                    field.ElementFullType = args[0].FullName ?? args[0].Name;
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

        // Dictionary
        if (name.Contains("Dictionary") && memberType.IsGenericType)
        {
            try
            {
                var args = memberType.GetGenericArguments();
                if (args.Length >= 2)
                {
                    var (kk, _, kp) = ClassifyType(args[0]);
                    var (vk, _, vp) = ClassifyType(args[1]);
                    field.KeyKind = kk;
                    field.KeyFullType = args[0].FullName ?? args[0].Name;
                    field.ValueKind = vk;
                    field.ValueFullType = args[1].FullName ?? args[1].Name;
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

// ====================================================================== //
// JSON data models
// ====================================================================== //

public sealed class MetadataRoot
{
    public Dictionary<string, PrototypeMetadata> Prototypes { get; set; } = new();
    public Dictionary<string, ComponentMetadata> Components { get; set; } = new();
    public Dictionary<string, DataDefinitionMetadata> DataDefinitions { get; set; } = new();
}

public sealed class PrototypeMetadata
{
    public string ClassName { get; set; } = "";
    public string YamlType { get; set; } = "";
    public bool Inheriting { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class ComponentMetadata
{
    public string ClassName { get; set; } = "";
    public string Name { get; set; } = "";
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class DataDefinitionMetadata
{
    public string ClassName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class FieldMetadata
{
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Type { get; set; } = "";
    public string FullType { get; set; } = "";
    public string FieldKind { get; set; } = "text";
    public bool Required { get; set; }
    public bool IsId { get; set; }
    public bool IsParent { get; set; }
    public bool IsAbstract { get; set; }
    public bool? AlwaysPushInheritance { get; set; }
    public bool? NeverPushInheritance { get; set; }
    public string? ProtoTypeArg { get; set; }
    public string[]? EnumValues { get; set; }

    // List element info
    public string? ElementKind { get; set; }
    public string? ElementFullType { get; set; }
    public string? ElementProtoTypeArg { get; set; }

    // Map key/value info
    public string? KeyKind { get; set; }
    public string? KeyFullType { get; set; }
    public string? KeyProtoTypeArg { get; set; }
    public string? ValueKind { get; set; }
    public string? ValueFullType { get; set; }
    public string? ValueProtoTypeArg { get; set; }

    // DataDefinition reference
    public bool? IsDataDefinition { get; set; }
    public string? DataDefinitionType { get; set; }
}
