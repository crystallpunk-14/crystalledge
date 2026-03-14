using System.IO;
using System.Linq;
using System.Reflection;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Manager.Definition;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI;

/// <summary>
/// Parses YAML prototype files into structured data suitable for rendering as cards.
/// Resolves inheritance by comparing local YAML fields against the post-inheritance
/// mapping from <see cref="IPrototypeManager"/>.
/// </summary>
public static class PrototypeDataParser
{
    /// <summary>
    /// Meta-keys that live in the prototype header, not in DataField rows.
    /// </summary>
    private static readonly HashSet<string> MetaKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        ParentDataFieldAttribute.Name,
        IdDataFieldAttribute.Name,
        AbstractDataFieldAttribute.Name
    };

    /// <summary>
    /// Parses a YAML file from disk into a list of prototype card data.
    /// </summary>
    /// <param name="absolutePath">Absolute filesystem path to the .yml file.</param>
    /// <param name="protoManager">Optional prototype manager for inheritance resolution.</param>
    /// <returns>List of parsed prototype data, one per prototype entry in the file.</returns>
    public static List<ParsedPrototype> ParseFile(string absolutePath, IPrototypeManager? protoManager)
    {
        var results = new List<ParsedPrototype>();

        string yamlText;
        try
        {
            yamlText = File.ReadAllText(absolutePath);
        }
        catch (Exception)
        {
            return results;
        }

        using var reader = new StringReader(yamlText);
        IEnumerable<DataNodeDocument> documents;

        try
        {
            documents = DataNodeParser.ParseYamlStream(reader);
        }
        catch (Exception)
        {
            // Malformed YAML – return empty
            return results;
        }

        foreach (var document in documents)
        {
            if (document.Root is not SequenceDataNode sequence)
                continue;

            foreach (var node in sequence)
            {
                if (node is not MappingDataNode localMapping)
                    continue;

                var parsed = ParsePrototype(localMapping, protoManager);
                if (parsed != null)
                    results.Add(parsed);
            }
        }

        return results;
    }

    private static ParsedPrototype? ParsePrototype(
        MappingDataNode protoNode,
        IPrototypeManager? protoManager)
    {
        // Extract meta fields
        if (!protoNode.TryGet<ValueDataNode>("type", out var typeNode))
            return null;

        var kind = typeNode.Value;

        var id = protoNode.TryGet<ValueDataNode>(IdDataFieldAttribute.Name, out var idNode)
            ? idNode.Value
            : "<no id>";

        string? parentStr = null;
        if (protoNode.TryGet<ValueDataNode>(ParentDataFieldAttribute.Name, out var parentValueNode))
        {
            parentStr = parentValueNode.Value;
        }
        else if (protoNode.TryGet<SequenceDataNode>(ParentDataFieldAttribute.Name, out var parentSeqNode))
        {
            var parents = new List<string>();
            foreach (var pNode in parentSeqNode)
            {
                if (pNode is ValueDataNode pv)
                    parents.Add(pv.Value);
            }

            parentStr = string.Join(", ", parents);
        }

        var isAbstract = protoNode.TryGet<ValueDataNode>(AbstractDataFieldAttribute.Name, out var absNode)
                         && absNode.AsBool();

        // Collect local field keys (the ones explicitly written in this YAML entry)
        var localKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, _) in protoNode)
        {
            if (!MetaKeys.Contains(key))
                localKeys.Add(key);
        }

        // Try to get the resolved (post-inheritance) mapping from the prototype manager
        MappingDataNode? resolvedMapping = null;
        Type? protoType = null;
        if (protoManager != null)
        {
            if (protoManager.TryGetKindType(kind, out protoType))
            {
                try
                {
                    protoManager.TryGetMapping(protoType, id, out resolvedMapping);
                }
                catch
                {
                    // Prototype not loaded or abstract-only – that's fine
                }
            }
        }

        // Build field list
        var fields = new List<EditorFieldData>();

        // Build a mapping of tag → (C# field type, required, order, inheritance) from reflection
        var fieldTypeMap = protoType != null
            ? GetDataFieldInfo(protoType)
            : new Dictionary<string, (Type Type, bool Required, int Order, InheritanceBehavior Inheritance)>();

        // 1) Fields from the resolved mapping (includes inherited)
        if (resolvedMapping != null)
        {
            foreach (var (key, value) in resolvedMapping)
            {
                if (MetaKeys.Contains(key))
                    continue;

                var isOverridden = localKeys.Contains(key);
                var displayValue = DataNodeToString(value);

                // Inherited value = value from resolved mapping if NOT overridden,
                // otherwise we need to figure out what the parent provides.
                // For simplicity: inherited = resolved value when not overridden,
                // otherwise inherited = resolved value with local removed (approximate).
                string inheritedValue;
                if (!isOverridden)
                {
                    inheritedValue = displayValue;
                }
                else
                {
                    // Try to find the value from resolved mapping minus local override.
                    // Since resolved = parents + local merged, the "inherited" part
                    // would be the resolved value if we removed the local key.
                    // For now we approximate: if the field exists in resolved, use empty
                    // as "unknown inherited" — a better approach would diff parent mappings.
                    inheritedValue = "";
                }

                Type? fieldType = null;
                var isRequired = false;
                var fieldOrder = int.MaxValue;
                var inheritance = InheritanceBehavior.Default;
                if (fieldTypeMap.TryGetValue(key, out var fieldInfo))
                {
                    fieldType = fieldInfo.Type;
                    isRequired = fieldInfo.Required;
                    fieldOrder = fieldInfo.Order;
                    inheritance = fieldInfo.Inheritance;
                }

                // For AlwaysPushInheritance fields (e.g. ComponentRegistry),
                // use the LOCAL value rather than the resolved (merged) value
                // to preserve correct inheritance semantics.
                var effectiveValue = displayValue;
                var resolvedDisplayValue = (string?)null;
                if (inheritance == InheritanceBehavior.Always && isOverridden)
                {
                    effectiveValue = protoNode.TryGet(key, out var localNode)
                        ? DataNodeToString(localNode)
                        : "";
                    resolvedDisplayValue = displayValue;
                }

                fields.Add(new EditorFieldData
                {
                    Name = key,
                    Value = effectiveValue,
                    IsOverridden = isOverridden,
                    FieldType = fieldType,
                    IsRequired = isRequired,
                    Order = fieldOrder,
                    Inheritance = inheritance,
                    InheritedValue = inheritedValue,
                    ResolvedValue = resolvedDisplayValue,
                });
            }
        }

        // 2) If we have the C# type, add any DataField-annotated members that
        //    aren't in the resolved mapping (still at C# default).
        if (protoType != null)
        {
            var existingKeys = new HashSet<string>(fields.Select(f => f.Name), StringComparer.Ordinal);

            foreach (var (tag, (type, required, tagOrder, tagInheritance)) in fieldTypeMap)
            {
                if (existingKeys.Contains(tag) || MetaKeys.Contains(tag))
                    continue;

                fields.Add(new EditorFieldData
                {
                    Name = tag,
                    Value = "",
                    IsOverridden = false,
                    FieldType = type,
                    IsRequired = required,
                    Order = tagOrder,
                    Inheritance = tagInheritance,
                    InheritedValue = "",
                });
            }
        }

        // Sort fields by C# declaration order
        fields.Sort((a, b) => a.Order.CompareTo(b.Order));

        return new ParsedPrototype
        {
            Kind = kind,
            Id = id,
            Parent = parentStr,
            IsAbstract = isAbstract,
            Fields = fields,
        };
    }

    /// <summary>
    /// Uses reflection to find all [DataField] tags and their C# types on a prototype type.
    /// Returns (Type fieldType, bool isRequired, int order) per tag.
    /// Order follows C# declaration: base class members first, then derived.
    /// </summary>
    public static Dictionary<string, (Type Type, bool Required, int Order, InheritanceBehavior Inheritance)> GetDataFieldInfo(Type type)
    {
        var result = new Dictionary<string, (Type, bool, int, InheritanceBehavior)>(StringComparer.Ordinal);

        // Walk the hierarchy bottom-up and collect members per level,
        // then assign indices top-down so base class members come first.
        var levels = new List<List<(string Tag, Type FieldType, bool Required, InheritanceBehavior Inheritance)>>();

        var current = type;
        while (current != null && current != typeof(object))
        {
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            var level = new List<(string, Type, bool, InheritanceBehavior)>();

            foreach (var field in current.GetFields(flags))
            {
                var attr = field.GetCustomAttribute<DataFieldAttribute>();
                if (attr == null)
                    continue;

                var tag = attr.Tag ?? AutoGenerateTag(field.Name);
                var inh = GetInheritanceBehavior(field);
                level.Add((tag, field.FieldType, attr.Required, inh));
            }

            foreach (var prop in current.GetProperties(flags))
            {
                var attr = prop.GetCustomAttribute<DataFieldAttribute>();
                if (attr == null)
                    continue;

                var tag = attr.Tag ?? AutoGenerateTag(prop.Name);
                var inh = GetInheritanceBehavior(prop);
                level.Add((tag, prop.PropertyType, attr.Required, inh));
            }

            levels.Add(level);
            current = current.BaseType;
        }

        // Reverse so base classes come first
        levels.Reverse();

        var order = 0;
        foreach (var level in levels)
        {
            foreach (var (tag, fieldType, required, inheritance) in level)
            {
                result.TryAdd(tag, (fieldType, required, order++, inheritance));
            }
        }

        return result;
    }

    /// <summary>
    /// Determines the inheritance behavior for a field/property member
    /// based on [AlwaysPushInheritance] and [NeverPushInheritance] attributes.
    /// </summary>
    private static InheritanceBehavior GetInheritanceBehavior(MemberInfo member)
    {
        if (member.GetCustomAttribute<AlwaysPushInheritanceAttribute>() != null)
            return InheritanceBehavior.Always;
        if (member.GetCustomAttribute<NeverPushInheritanceAttribute>() != null)
            return InheritanceBehavior.Never;
        return InheritanceBehavior.Default;
    }

    /// <summary>
    /// <summary>
    /// Mirrors <see cref="DataDefinitionUtility.AutoGenerateTag"/> – lowercase first char.
    /// </summary>
    private static string AutoGenerateTag(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return $"{char.ToLowerInvariant(name[0])}{name.AsSpan(1).ToString()}";
    }

    /// <summary>
    /// Converts a DataNode tree to a human-readable text representation.
    /// For now all values are shown as plain text.
    /// </summary>
    public static string DataNodeToString(DataNode node)
    {
        return node switch
        {
            ValueDataNode valueNode => valueNode.Value,
            MappingDataNode mappingNode => MappingToString(mappingNode),
            SequenceDataNode seqNode => SequenceToString(seqNode),
            _ => node.ToString() ?? "",
        };
    }

    private static string MappingToString(MappingDataNode mapping)
    {
        var parts = new List<string>();
        foreach (var (key, value) in mapping)
        {
            parts.Add($"{key}: {DataNodeToString(value)}");
        }

        return $"{{ {string.Join(", ", parts)} }}";
    }

    private static string SequenceToString(SequenceDataNode sequence)
    {
        var parts = new List<string>();
        foreach (var item in sequence)
        {
            parts.Add(DataNodeToString(item));
        }

        return $"[{string.Join(", ", parts)}]";
    }
}

/// <summary>
/// Parsed prototype data ready for rendering as a card.
/// </summary>
public sealed class ParsedPrototype
{
    public string Kind { get; init; } = "";
    public string Id { get; init; } = "";
    public string? Parent { get; init; }
    public bool IsAbstract { get; init; }
    public List<EditorFieldData> Fields { get; init; } = new();
}
