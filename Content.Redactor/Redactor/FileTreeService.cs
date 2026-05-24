using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.Redactor.Redactor;

/// <summary>
/// Builds the JSON-serializable directory/file tree used by the editor's left
/// pane. Filters non-YAML leaf files and sorts entries alphabetically.
/// </summary>
internal static class FileTreeService
{
    public static List<FileTreeNode> Build(string baseDir, string relativePath = "", string pathPrefix = "")
    {
        var fullPath = string.IsNullOrEmpty(relativePath) ? baseDir : Path.Combine(baseDir, relativePath);
        if (!Directory.Exists(fullPath)) return new();

        var nodes = new List<FileTreeNode>();

        foreach (var dir in Directory.GetDirectories(fullPath).OrderBy(d => d))
        {
            var name = Path.GetFileName(dir);
            var rel = string.IsNullOrEmpty(relativePath) ? name : $"{relativePath}/{name}";
            nodes.Add(new FileTreeNode
            {
                Name = name,
                Path = pathPrefix + rel,
                IsDir = true,
                Children = Build(baseDir, rel, pathPrefix),
            });
        }

        foreach (var file in Directory.GetFiles(fullPath)
                     .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            var rel = string.IsNullOrEmpty(relativePath) ? name : $"{relativePath}/{name}";
            nodes.Add(new FileTreeNode { Name = name, Path = pathPrefix + rel, IsDir = false });
        }

        return nodes;
    }

    public static void MarkReadOnly(List<FileTreeNode> nodes)
    {
        foreach (var n in nodes)
        {
            n.ReadOnly = true;
            if (n.Children != null) MarkReadOnly(n.Children);
        }
    }
}
