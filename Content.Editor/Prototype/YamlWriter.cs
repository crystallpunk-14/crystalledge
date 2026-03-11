using System.IO;
using System.Text;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Editor.Prototype;

/// <summary>
/// Handles serializing DataNode trees back to YAML text,
/// using the engine's own DataNode types for round-trip fidelity.
/// </summary>
public static class YamlWriter
{
    /// <summary>
    /// Serializes a list of prototype entries back to YAML text.
    /// Preserves the original document structure as closely as possible.
    /// </summary>
    public static string SerializeToYaml(IReadOnlyList<PrototypeEntry> entries)
    {
        var sb = new StringBuilder();

        foreach (var entry in entries)
        {
            if (entry.Mapping == null)
                continue;

            sb.AppendLine(SerializeMapping(entry.Mapping, 0, true));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the YAML content to a file on disk.
    /// </summary>
    public static void SaveToFile(string absolutePath, string yamlContent)
    {
        // Normalize line endings to LF (consistent with SS14 convention)
        yamlContent = yamlContent.Replace("\r\n", "\n");
        File.WriteAllText(absolutePath, yamlContent, new UTF8Encoding(false));
    }

    private static string SerializeMapping(MappingDataNode mapping, int indent, bool isListItem)
    {
        var sb = new StringBuilder();
        var prefix = new string(' ', indent);
        var first = true;

        foreach (var (key, value) in mapping)
        {
            if (first && isListItem)
            {
                sb.Append($"{prefix}- {key}: ");
                first = false;
            }
            else
            {
                sb.Append($"{prefix}  {key}: ");
            }

            switch (value)
            {
                case ValueDataNode valueNode:
                    var text = valueNode.Value;
                    // Check if we need to quote the value
                    if (NeedsQuoting(text))
                        sb.AppendLine($"\"{EscapeYamlString(text)}\"");
                    else
                        sb.AppendLine(text);
                    break;

                case MappingDataNode nestedMapping:
                    sb.AppendLine();
                    sb.Append(SerializeNestedMapping(nestedMapping, indent + 4));
                    break;

                case SequenceDataNode sequence:
                    sb.AppendLine();
                    sb.Append(SerializeSequence(sequence, indent + 4));
                    break;

                default:
                    sb.AppendLine(value?.ToString() ?? "");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string SerializeNestedMapping(MappingDataNode mapping, int indent)
    {
        var sb = new StringBuilder();
        var prefix = new string(' ', indent);

        foreach (var (key, value) in mapping)
        {
            sb.Append($"{prefix}{key}: ");

            switch (value)
            {
                case ValueDataNode valueNode:
                    var text = valueNode.Value;
                    if (NeedsQuoting(text))
                        sb.AppendLine($"\"{EscapeYamlString(text)}\"");
                    else
                        sb.AppendLine(text);
                    break;

                case MappingDataNode nested:
                    sb.AppendLine();
                    sb.Append(SerializeNestedMapping(nested, indent + 2));
                    break;

                case SequenceDataNode seq:
                    sb.AppendLine();
                    sb.Append(SerializeSequence(seq, indent + 2));
                    break;

                default:
                    sb.AppendLine(value?.ToString() ?? "");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string SerializeSequence(SequenceDataNode sequence, int indent)
    {
        var sb = new StringBuilder();
        var prefix = new string(' ', indent);

        foreach (var item in sequence.Sequence)
        {
            switch (item)
            {
                case ValueDataNode valueNode:
                    var text = valueNode.Value;
                    if (NeedsQuoting(text))
                        sb.AppendLine($"{prefix}- \"{EscapeYamlString(text)}\"");
                    else
                        sb.AppendLine($"{prefix}- {text}");
                    break;

                case MappingDataNode mapping:
                    sb.Append(SerializeMapping(mapping, indent, true));
                    break;

                case SequenceDataNode nested:
                    sb.AppendLine($"{prefix}-");
                    sb.Append(SerializeSequence(nested, indent + 2));
                    break;

                default:
                    sb.AppendLine($"{prefix}- {item}");
                    break;
            }
        }

        return sb.ToString();
    }

    private static bool NeedsQuoting(string value)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        // Quote if it contains special YAML characters
        if (value.Contains(':') || value.Contains('#') || value.Contains('{') ||
            value.Contains('}') || value.Contains('[') || value.Contains(']') ||
            value.Contains(',') || value.Contains('&') || value.Contains('*') ||
            value.Contains('!') || value.Contains('|') || value.Contains('>') ||
            value.Contains('\'') || value.Contains('"') || value.Contains('%') ||
            value.Contains('@') || value.Contains('`'))
            return true;

        // Quote if it starts or ends with whitespace
        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
            return true;

        // Quote booleans that shouldn't be interpreted as YAML booleans
        var lower = value.ToLowerInvariant();
        if (lower is "true" or "false" or "yes" or "no" or "on" or "off" or "null" or "~")
            return true;

        return false;
    }

    private static string EscapeYamlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
