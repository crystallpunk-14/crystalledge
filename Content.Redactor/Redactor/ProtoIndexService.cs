using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.Redactor.Redactor;

/// <summary>
/// Owns the in-memory prototype index. Discovers prototypes across the content
/// prototypes directory and (optionally) a separate read-only engine prototypes
/// directory. Supports incremental refresh and ID search.
/// </summary>
internal sealed class ProtoIndexService
{
    private readonly string _prototypesDir;
    private readonly string _enginePrototypesDir;
    private Dictionary<string, List<ProtoIndexEntry>> _index = new();

    public const string EnginePrefix = "__engine__/";

    public ProtoIndexService(string prototypesDir, string enginePrototypesDir)
    {
        _prototypesDir = prototypesDir;
        _enginePrototypesDir = enginePrototypesDir;
    }

    public IReadOnlyDictionary<string, List<ProtoIndexEntry>> Index => _index;
    public int TotalCount => _index.Values.Sum(l => l.Count);
    public int TypeCount => _index.Count;

    public void Rebuild()
    {
        var idx = Build(_prototypesDir);
        if (Directory.Exists(_enginePrototypesDir))
        {
            var engineIndex = Build(_enginePrototypesDir, readOnly: true, pathPrefix: EnginePrefix);
            foreach (var (type, entries) in engineIndex)
            {
                if (!idx.ContainsKey(type)) idx[type] = new List<ProtoIndexEntry>();
                idx[type].AddRange(entries);
            }
        }
        _index = idx;
    }

    /// <summary>
    /// Rebuilds index entries that originate from a single file. Existing entries
    /// for that file are removed first so renames/deletions are reflected.
    /// </summary>
    public void RefreshFile(string fullPath, string relativePath)
    {
        foreach (var list in _index.Values)
            list.RemoveAll(e => e.File == relativePath);

        try { YamlPrototypeScanner.Scan(fullPath, relativePath, _index); }
        catch { /* ignore */ }
    }

    public List<ProtoSearchResult> Search(string type, string query, int limit)
    {
        if (!_index.TryGetValue(type, out var entries))
            return new();

        if (string.IsNullOrWhiteSpace(query))
            return entries.Take(limit).Select(e => new ProtoSearchResult { Id = e.Id, Name = e.Name }).ToList();

        var lower = query.ToLowerInvariant();
        var prefix = entries
            .Where(e => e.Id.ToLowerInvariant().StartsWith(lower))
            .Select(e => new ProtoSearchResult { Id = e.Id, Name = e.Name });
        var contains = entries
            .Where(e => !e.Id.ToLowerInvariant().StartsWith(lower) &&
                        (e.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         (e.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Select(e => new ProtoSearchResult { Id = e.Id, Name = e.Name });

        return prefix.Concat(contains).Take(limit).ToList();
    }

    private static Dictionary<string, List<ProtoIndexEntry>> Build(string root, bool readOnly = false, string pathPrefix = "")
    {
        var index = new Dictionary<string, List<ProtoIndexEntry>>();
        if (!Directory.Exists(root)) return index;

        var files = Directory.GetFiles(root, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(root, "*.yaml", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            try
            {
                var rel = pathPrefix + Path.GetRelativePath(root, file).Replace('\\', '/');
                YamlPrototypeScanner.Scan(file, rel, index, readOnly);
            }
            catch { /* skip unreadable */ }
        }
        return index;
    }
}
