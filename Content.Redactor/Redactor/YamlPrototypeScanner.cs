using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Redactor.Redactor;

/// <summary>
/// Parses an SS14 prototype YAML file using YamlDotNet's representation model
/// and yields one <see cref="ProtoIndexEntry"/> per prototype declaration.
/// Robust to comments, multi-line strings, anchors, and custom <c>!type:</c> tags.
/// </summary>
internal static class YamlPrototypeScanner
{
    /// <summary>
    /// Read a prototype file and add discovered prototype declarations to
    /// the supplied index. Silently swallows YAML parse errors so a single bad
    /// file does not break the whole index build.
    /// </summary>
    public static void Scan(
        string filePath,
        string relativePath,
        Dictionary<string, List<ProtoIndexEntry>> index,
        bool readOnly = false)
    {
        YamlStream yaml;
        try
        {
            using var reader = new StreamReader(filePath);
            yaml = new YamlStream();
            yaml.Load(reader);
        }
        catch (YamlException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var doc in yaml.Documents)
        {
            if (doc.RootNode is not YamlSequenceNode rootSeq)
                continue;

            foreach (var item in rootSeq.Children)
            {
                if (item is not YamlMappingNode mapping)
                    continue;

                var type = GetScalar(mapping, "type");
                var id = GetScalar(mapping, "id");
                if (type == null || id == null)
                    continue;

                var name = GetScalar(mapping, "name");
                var parents = GetParents(mapping);
                var abstractFlag = string.Equals(GetScalar(mapping, "abstract"), "true", StringComparison.OrdinalIgnoreCase);

                if (!index.TryGetValue(type, out var list))
                {
                    list = new List<ProtoIndexEntry>();
                    index[type] = list;
                }

                list.Add(new ProtoIndexEntry
                {
                    Id = id,
                    Name = name,
                    File = relativePath,
                    Parents = parents,
                    Abstract = abstractFlag,
                    ReadOnly = readOnly,
                });
            }
        }
    }

    private static string? GetScalar(YamlMappingNode mapping, string key)
    {
        foreach (var (k, v) in mapping.Children)
        {
            if (k is YamlScalarNode ks && ks.Value == key && v is YamlScalarNode vs)
                return vs.Value;
        }
        return null;
    }

    private static string[]? GetParents(YamlMappingNode mapping)
    {
        foreach (var (k, v) in mapping.Children)
        {
            if (k is not YamlScalarNode ks || ks.Value != "parent")
                continue;

            switch (v)
            {
                case YamlScalarNode scalar when !string.IsNullOrEmpty(scalar.Value):
                    return new[] { scalar.Value! };
                case YamlSequenceNode seq:
                    var result = new List<string>(seq.Children.Count);
                    foreach (var entry in seq.Children)
                    {
                        if (entry is YamlScalarNode s && !string.IsNullOrEmpty(s.Value))
                            result.Add(s.Value!);
                    }
                    return result.Count == 0 ? null : result.ToArray();
            }
        }
        return null;
    }
}
