using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.Manager;

namespace Content.Editor.Prototype;

/// <summary>
/// Parses YAML documents into prototype entries using the engine's DataNode system.
/// This provides 100% compatibility with the engine's own prototype loading pipeline.
/// </summary>
public static class PrototypeParser
{
    /// <summary>
    /// Extracts prototype entries from parsed YAML documents.
    /// Uses the engine's real IPrototypeManager to resolve prototype kinds.
    /// </summary>
    public static List<PrototypeEntry> ExtractEntries(
        IEnumerable<DataNodeDocument> documents,
        IPrototypeManager prototypeManager)
    {
        var entries = new List<PrototypeEntry>();

        foreach (var doc in documents)
        {
            if (doc.Root is not SequenceDataNode sequence)
                continue;

            foreach (var node in sequence.Sequence)
            {
                if (node is not MappingDataNode mapping)
                    continue;

                var entry = ExtractEntry(mapping, prototypeManager);
                if (entry != null)
                    entries.Add(entry);
            }
        }

        return entries;
    }

    private static PrototypeEntry? ExtractEntry(MappingDataNode mapping, IPrototypeManager prototypeManager)
    {
        // Read "type" field -- required for all prototypes
        if (!mapping.TryGet("type", out var typeNode) || typeNode is not ValueDataNode typeValue)
            return null;

        var typeString = typeValue.Value;

        // Try to resolve the prototype C# type from the manager
        Type? prototypeType = null;
        try
        {
            foreach (var kindType in prototypeManager.EnumeratePrototypeKinds())
            {
                var name = ResolvePrototypeTypeName(kindType);

                if (name == typeString)
                {
                    prototypeType = kindType;
                    break;
                }
            }
        }
        catch
        {
            // Fall through -- type stays null
        }

        // Read "id" field
        string? id = null;
        if (mapping.TryGet("id", out var idNode) && idNode is ValueDataNode idValue)
            id = idValue.Value;

        // Read "parent" field
        string[]? parents = null;
        if (mapping.TryGet("parent", out var parentNode))
        {
            parents = parentNode switch
            {
                ValueDataNode valueNode => [valueNode.Value],
                SequenceDataNode seqNode => seqNode.Sequence
                    .OfType<ValueDataNode>()
                    .Select(v => v.Value)
                    .ToArray(),
                _ => null,
            };
        }

        // Read "abstract" field
        var isAbstract = false;
        if (mapping.TryGet("abstract", out var abstractNode) && abstractNode is ValueDataNode abstractValue)
            bool.TryParse(abstractValue.Value, out isAbstract);

        return new PrototypeEntry
        {
            TypeString = typeString,
            PrototypeType = prototypeType,
            Id = id,
            Parents = parents,
            IsAbstract = isAbstract,
            Mapping = mapping,
        };
    }

    /// <summary>
    /// Resolves the YAML type name for a prototype C# type,
    /// matching the engine's own logic: attr.Type ?? CalculatePrototypeName.
    /// </summary>
    public static string ResolvePrototypeTypeName(Type kindType)
    {
        var attr = (PrototypeAttribute?)Attribute.GetCustomAttribute(kindType, typeof(PrototypeAttribute));
        return attr?.Type ?? PrototypeUtility.CalculatePrototypeName(kindType.Name);
    }
}

/// <summary>
/// Represents a single prototype entry parsed from a YAML file.
/// Holds both the raw DataNode mapping and extracted metadata.
/// </summary>
public sealed class PrototypeEntry
{
    /// <summary>
    /// The YAML "type" field value (e.g., "entity", "reagent").
    /// </summary>
    public string TypeString = "";

    /// <summary>
    /// The resolved C# type for this prototype kind, or null if unknown.
    /// </summary>
    public Type? PrototypeType;

    /// <summary>
    /// The prototype ID, or null if not specified.
    /// </summary>
    public string? Id;

    /// <summary>
    /// Parent prototype IDs for inheritance.
    /// </summary>
    public string[]? Parents;

    /// <summary>
    /// Whether this is an abstract prototype.
    /// </summary>
    public bool IsAbstract;

    /// <summary>
    /// The raw MappingDataNode from the engine's YAML parser.
    /// </summary>
    public MappingDataNode? Mapping;

    /// <summary>
    /// Updates a field in the mapping.
    /// Creates DataNodes directly for common types to match YAML conventions
    /// (lowercase booleans, unquoted numbers, etc.).
    /// </summary>
    public void UpdateField(string fieldTag, object? newValue, ISerializationManager serializationManager)
    {
        if (Mapping == null)
            return;

        if (newValue == null)
        {
            Mapping.Remove(fieldTag);
            return;
        }

        // If already a DataNode, use directly
        if (newValue is DataNode dataNode)
        {
            Mapping[fieldTag] = dataNode;
            return;
        }

        // Common types: create ValueDataNode with YAML-correct formatting
        if (newValue is bool boolVal)
        {
            Mapping[fieldTag] = new ValueDataNode(boolVal ? "true" : "false");
            return;
        }

        if (newValue is int or long or short or byte)
        {
            Mapping[fieldTag] = new ValueDataNode(
                Convert.ToString(newValue, System.Globalization.CultureInfo.InvariantCulture) ?? "0");
            return;
        }

        if (newValue is float or double or decimal)
        {
            Mapping[fieldTag] = new ValueDataNode(
                Convert.ToString(newValue, System.Globalization.CultureInfo.InvariantCulture) ?? "0");
            return;
        }

        if (newValue is string strVal)
        {
            Mapping[fieldTag] = new ValueDataNode(strVal);
            return;
        }

        // Fallback: use engine serialization for complex types
        var valueNode = serializationManager.WriteValue(newValue.GetType(), newValue);
        Mapping[fieldTag] = valueNode;
    }
}
