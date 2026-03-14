using System.IO;
using System.Linq;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Editor.UI;

/// <summary>
/// Writes pending edits and resets back into a YAML prototype file on disk.
/// Re-parses the original file's DataNode tree, applies changes, and serializes.
/// </summary>
public static class PrototypeYamlWriter
{
    /// <summary>
    /// Applies pending edits and resets to the YAML file on disk.
    /// </summary>
    /// <param name="filePath">Absolute path to the .yml file.</param>
    /// <param name="session">The edit session containing pending changes.</param>
    /// <returns>True if the file was written successfully.</returns>
    public static bool Save(string filePath, FileEditSession session)
    {
        // 1. Read the original YAML
        string yamlText;
        try
        {
            yamlText = File.ReadAllText(filePath);
        }
        catch
        {
            return false;
        }

        // 2. Parse into DataNode tree
        using var reader = new StringReader(yamlText);
        List<DataNodeDocument> documents;
        try
        {
            documents = DataNodeParser.ParseYamlStream(reader).ToList();
        }
        catch
        {
            return false;
        }

        // 3. Apply pending edits and resets to the DataNode tree
        foreach (var document in documents)
        {
            if (document.Root is not SequenceDataNode sequence)
                continue;

            for (var i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] is not MappingDataNode mapping)
                    continue;

                // Get prototype ID
                if (!mapping.TryGet<ValueDataNode>("id", out var idNode))
                    continue;

                var protoId = idNode.Value;
                ApplyEditsToMapping(mapping, protoId, session);
            }
        }

        // 4. Serialize back to YAML and write to disk
        try
        {
            using var writer = new StreamWriter(filePath, append: false);

            foreach (var document in documents)
            {
                if (document.Root != null)
                {
                    document.Root.Write(writer);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyEditsToMapping(
        MappingDataNode mapping,
        string protoId,
        FileEditSession session)
    {
        // Apply resets — remove keys from the mapping
        var keysToRemove = new List<string>();
        foreach (var (key, _) in mapping)
        {
            var editKey = (protoId, key);
            if (session.PendingResets.Contains(editKey))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            mapping.Remove(key);
        }

        // Apply edits — update or insert keys in the mapping
        foreach (var ((pendingProtoId, fieldName), newValue) in session.PendingEdits)
        {
            if (pendingProtoId != protoId)
                continue;

            // Remove existing key first (to replace), then add
            mapping.Remove(fieldName);
            mapping.Add(fieldName, new ValueDataNode(newValue));
        }
    }
}
