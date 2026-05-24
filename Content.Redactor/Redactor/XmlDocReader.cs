using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Content.Redactor.Redactor;

/// <summary>
/// Reads XML documentation files (produced by the compiler) and provides
/// lookup for type and member summaries.
/// </summary>
public sealed class XmlDocReader
{
    private readonly Dictionary<string, string> _docs = new(StringComparer.Ordinal);

    public int Count => _docs.Count;

    /// <summary>
    /// Load all .xml doc files from the specified directory.
    /// Entries accumulate across multiple calls (server and client bin dirs both contribute).
    /// </summary>
    public void LoadFromDirectory(string directory)
    {
        foreach (var xmlPath in Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var xdoc = XDocument.Load(xmlPath);
                foreach (var member in xdoc.Descendants("member"))
                {
                    var nameAttr = member.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(nameAttr))
                        continue;
                    var summary = member.Element("summary")?.Value;
                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        _docs[nameAttr] = summary.Trim().Replace("\r\n", " ").Replace("\n", " ");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Redactor] Warning: Could not parse XML doc {Path.GetFileName(xmlPath)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get the summary for a fully-qualified type.
    /// </summary>
    public string? GetTypeSummary(Type type)
    {
        var fullName = type.FullName?.Replace('+', '.');
        if (fullName == null)
            return null;
        return _docs.GetValueOrDefault($"T:{fullName}");
    }

    /// <summary>
    /// Get the summary for a field or property member.
    /// </summary>
    public string? GetMemberSummary(System.Reflection.MemberInfo member)
    {
        var typeName = member.DeclaringType?.FullName?.Replace('+', '.');
        if (typeName == null)
            return null;
        var prefix = member is System.Reflection.PropertyInfo ? "P" : "F";
        return _docs.GetValueOrDefault($"{prefix}:{typeName}.{member.Name}");
    }
}
