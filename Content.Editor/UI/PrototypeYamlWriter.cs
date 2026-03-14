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
            // Serialize each prototype entry individually, strip YAML document
            // markers (--- / ...), and join with blank lines between prototypes.
            var protoTexts = new List<string>();
            foreach (var document in documents)
            {
                if (document.Root is not SequenceDataNode sequence)
                    continue;

                for (var i = 0; i < sequence.Count; i++)
                {
                    var entry = sequence[i];
                    using var sw = new StringWriter();

                    // Wrap single mapping in a sequence so Write() produces "- type: ..." format
                    var wrapper = new SequenceDataNode();
                    wrapper.Add(entry);
                    wrapper.Write(sw);

                    var text = sw.ToString().Trim();

                    // DataNode.Write() emits YAML document markers — strip them
                    if (text.EndsWith("..."))
                        text = text[..^3].TrimEnd();
                    if (text.StartsWith("---"))
                        text = text[3..].TrimStart('\r', '\n');

                    protoTexts.Add(text);
                }
            }

            var result = string.Join("\n\n", protoTexts);
            // Ensure single trailing newline
            result = result.TrimEnd() + "\n";

            File.WriteAllText(filePath, result);
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
        // ── Prototype-level field resets — remove keys from the mapping ──
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

        // ── Prototype-level field edits — update or insert keys ──
        foreach (var ((pendingProtoId, fieldName), newValue) in session.PendingEdits)
        {
            if (pendingProtoId != protoId)
                continue;

            mapping.Remove(fieldName);
            mapping.Add(fieldName, ParseValueToDataNode(newValue));
        }
    }

    /// <summary>
    /// Converts an editor value string into the appropriate DataNode.
    /// Strings starting with '[' become SequenceDataNode,
    /// strings starting with '{' become MappingDataNode,
    /// everything else becomes ValueDataNode.
    /// </summary>
    private static DataNode ParseValueToDataNode(string value)
    {
        var trimmed = value.Trim();

        // Sequence: [item1, item2, ...]
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            var inner = trimmed[1..^1].Trim();
            var sequence = new SequenceDataNode();

            if (!string.IsNullOrEmpty(inner))
            {
                var elements = SplitRespectingNesting(inner, ',');
                foreach (var element in elements)
                {
                    var el = element.Trim();
                    if (!string.IsNullOrEmpty(el))
                        sequence.Add(ParseValueToDataNode(el));
                }
            }

            return sequence;
        }

        // Mapping: { key1: val1, key2: val2 }
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            var inner = trimmed[1..^1].Trim();
            var mappingNode = new MappingDataNode();

            if (!string.IsNullOrEmpty(inner))
            {
                var entries = SplitRespectingNesting(inner, ',');
                foreach (var entry in entries)
                {
                    var colonIdx = entry.IndexOf(':');
                    if (colonIdx >= 0)
                    {
                        var k = entry[..colonIdx].Trim();
                        var v = entry[(colonIdx + 1)..].Trim();
                        if (!string.IsNullOrEmpty(k))
                            mappingNode.Add(k, ParseValueToDataNode(v));
                    }
                }
            }

            return mappingNode;
        }

        return new ValueDataNode(trimmed);
    }

    /// <summary>
    /// Splits a string by a delimiter while respecting nested brackets and braces.
    /// </summary>
    private static List<string> SplitRespectingNesting(string text, char delimiter)
    {
        var result = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();

        foreach (var ch in text)
        {
            if (ch is '[' or '{')
                depth++;
            else if (ch is ']' or '}')
                depth--;

            if (ch == delimiter && depth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        var last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
            result.Add(last);

        return result;
    }
}
