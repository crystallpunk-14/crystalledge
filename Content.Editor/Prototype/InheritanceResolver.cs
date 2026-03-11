using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.Prototype;

/// <summary>
/// Resolves prototype inheritance by walking parent chains.
/// Determines whether each field is local, inherited, or default.
/// </summary>
public static class InheritanceResolver
{
    /// <summary>
    /// Resolves the merged parent data for a prototype entry.
    /// Uses IPrototypeManager.TryGetMapping to get post-inheritance parent data.
    /// </summary>
    public static MappingDataNode? ResolveParentData(
        PrototypeEntry entry,
        IPrototypeManager prototypeManager)
    {
        if (entry.Parents == null || entry.Parents.Length == 0 || entry.PrototypeType == null)
            return null;

        MappingDataNode? merged = null;

        foreach (var parentId in entry.Parents)
        {
            try
            {
                if (!prototypeManager.TryGetMapping(entry.PrototypeType, parentId, out var parentMapping))
                    continue;

                if (merged == null)
                {
                    merged = parentMapping.Copy() as MappingDataNode;
                }
                else
                {
                    merged = DeepMerge(merged!, parentMapping);
                }
            }
            catch
            {
                // Parent might not exist yet (e.g., new prototype)
            }
        }

        return merged;
    }

    /// <summary>
    /// Determines the source of a field value.
    /// </summary>
    public static FieldSource GetFieldSource(
        string tag,
        MappingDataNode? localMapping,
        MappingDataNode? inheritedMapping)
    {
        if (localMapping != null && localMapping.Has(tag))
            return FieldSource.Local;

        if (inheritedMapping != null && inheritedMapping.Has(tag))
            return FieldSource.Inherited;

        return FieldSource.Default;
    }

    /// <summary>
    /// Gets the display DataNode for a field, considering local, inherited, and default sources.
    /// </summary>
    public static DataNode? GetFieldValue(
        string tag,
        MappingDataNode? localMapping,
        MappingDataNode? inheritedMapping)
    {
        if (localMapping != null && localMapping.TryGet(tag, out var localNode))
            return localNode;

        if (inheritedMapping != null && inheritedMapping.TryGet(tag, out var inheritedNode))
            return inheritedNode;

        return null;
    }

    /// <summary>
    /// Deep-merges two MappingDataNodes. Source values override target for scalar keys.
    /// Mappings are recursively merged. Sequences from source replace target.
    /// Special handling for "components" key (merge by type).
    /// </summary>
    public static MappingDataNode DeepMerge(MappingDataNode target, MappingDataNode source)
    {
        var result = target.Copy() as MappingDataNode ?? new MappingDataNode();

        foreach (var (key, sourceValue) in source)
        {
            if (key == "components")
            {
                // Special component array merge
                if (result.TryGet(key, out var existingNode)
                    && existingNode is SequenceDataNode existingSeq
                    && sourceValue is SequenceDataNode sourceSeq)
                {
                    result[key] = MergeComponentArrays(existingSeq, sourceSeq);
                    continue;
                }
            }

            if (result.TryGet(key, out var targetValue)
                && targetValue is MappingDataNode targetMap
                && sourceValue is MappingDataNode sourceMap)
            {
                // Recursive merge for nested mappings
                result[key] = DeepMerge(targetMap, sourceMap);
            }
            else
            {
                // Overwrite with source value
                result[key] = sourceValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges two component arrays by component "type" key.
    /// Components from source override same-type components in target.
    /// </summary>
    private static SequenceDataNode MergeComponentArrays(
        SequenceDataNode target,
        SequenceDataNode source)
    {
        // Build dict of target components by type
        var byType = new Dictionary<string, MappingDataNode>();
        var order = new List<string>();

        foreach (var node in target.Sequence.OfType<MappingDataNode>())
        {
            if (node.TryGet("type", out var typeNode) && typeNode is ValueDataNode typeVal)
            {
                byType[typeVal.Value] = node;
                if (!order.Contains(typeVal.Value))
                    order.Add(typeVal.Value);
            }
        }

        // Merge/add source components
        foreach (var node in source.Sequence.OfType<MappingDataNode>())
        {
            if (node.TryGet("type", out var typeNode) && typeNode is ValueDataNode typeVal)
            {
                if (byType.TryGetValue(typeVal.Value, out var existing))
                {
                    byType[typeVal.Value] = DeepMerge(existing, node);
                }
                else
                {
                    byType[typeVal.Value] = node;
                    order.Add(typeVal.Value);
                }
            }
        }

        // Rebuild sequence in order
        var result = new SequenceDataNode();
        foreach (var typeName in order)
        {
            if (byType.TryGetValue(typeName, out var mapping))
                result.Add(mapping);
        }

        return result;
    }
}
