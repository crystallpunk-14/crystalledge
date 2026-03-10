using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Content.Redactor.Redactor;

/// <summary>
/// Lightweight HTTP server that serves the Redactor web UI and provides
/// REST-ish API endpoints for file browsing, reading/writing YAML, and
/// searching prototype IDs.
/// </summary>
public static class RedactorServer
{
    private static string _solutionRoot = "";
    private static string _redactorDir = "";
    private static string _prototypesDir = "";
    private static Dictionary<string, List<ProtoIndexEntry>> _protoIndex = new();

    public static async Task StartAsync(string solutionRoot, int port)
    {
        _solutionRoot = solutionRoot;
        _redactorDir = Path.Combine(solutionRoot, "Redactor");
        _prototypesDir = Path.Combine(solutionRoot, "Resources", "Prototypes");

        Console.WriteLine("[Redactor] Building prototype index...");
        _protoIndex = BuildProtoIndex(_prototypesDir);
        Console.WriteLine($"[Redactor] Indexed {_protoIndex.Values.Sum(l => l.Count)} prototypes across {_protoIndex.Count} types");

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        Console.WriteLine($"[Redactor] Editor running at http://localhost:{port}/");
        Console.WriteLine("[Redactor] Press Ctrl+C to stop.");

        TryOpenBrowser($"http://localhost:{port}/");

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            _ = Task.Run(() => HandleRequestAsync(ctx));
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 200;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath ?? "/";
            if (path.StartsWith("/api/"))
                await HandleApiAsync(path, req, res);
            else
                await ServeStaticAsync(path, res);
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            res.ContentType = "application/json";
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            await res.OutputStream.WriteAsync(body);
        }
        finally
        {
            res.Close();
        }
    }

    private static async Task HandleApiAsync(string path, HttpListenerRequest req, HttpListenerResponse res)
    {
        res.ContentType = "application/json; charset=utf-8";

        switch (path)
        {
            case "/api/tree":
                await WriteJsonAsync(res, BuildFileTree(_prototypesDir, ""));
                break;

            case "/api/file":
                await HandleFileEndpointAsync(req, res);
                break;

            case "/api/metadata":
                await ServeMetadataAsync(res);
                break;

            case "/api/proto-index":
                await WriteJsonAsync(res, _protoIndex);
                break;

            case "/api/search-protos":
            {
                var q = req.QueryString["q"] ?? "";
                var type = req.QueryString["type"] ?? "entity";
                var limit = int.TryParse(req.QueryString["limit"], out var l) ? l : 50;
                await WriteJsonAsync(res, SearchProtos(type, q, limit));
                break;
            }

            case "/api/refresh-index":
                _protoIndex = BuildProtoIndex(_prototypesDir);
                await WriteJsonAsync(res, new { count = _protoIndex.Values.Sum(x => x.Count) });
                break;

            case "/api/open-in-explorer":
            {
                var relPath = req.QueryString["path"];
                if (!string.IsNullOrEmpty(relPath))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(_prototypesDir, relPath));
                    if (fullPath.StartsWith(Path.GetFullPath(_prototypesDir)))
                    {
                        var target = File.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath) ?? fullPath;
                        try
                        {
                            if (OperatingSystem.IsWindows())
                                Process.Start("explorer.exe", $"/select,\"{target}\"");
                            else if (OperatingSystem.IsMacOS())
                                Process.Start("open", $"-R \"{target}\"");
                            else
                                Process.Start("xdg-open", Path.GetDirectoryName(target) ?? target);
                        }
                        catch { /* non-critical */ }
                    }
                }
                await WriteJsonAsync(res, new { success = true });
                break;
            }

            case "/api/open-default":
            {
                var relPath = req.QueryString["path"];
                if (!string.IsNullOrEmpty(relPath))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(_prototypesDir, relPath));
                    if (fullPath.StartsWith(Path.GetFullPath(_prototypesDir)) && File.Exists(fullPath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fullPath,
                                UseShellExecute = true, // opens with default app
                            });
                        }
                        catch { /* non-critical */ }
                    }
                }
                await WriteJsonAsync(res, new { success = true });
                break;
            }

            case "/api/rename-file":
            {
                using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                var bodyStr = await reader.ReadToEndAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(bodyStr);
                var oldRel = doc.GetProperty("oldPath").GetString()!;
                var newName = doc.GetProperty("newName").GetString()!;
                var oldFull = Path.GetFullPath(Path.Combine(_prototypesDir, oldRel));
                var newFull = Path.Combine(Path.GetDirectoryName(oldFull)!, newName);

                if (!oldFull.StartsWith(Path.GetFullPath(_prototypesDir)) ||
                    !newFull.StartsWith(Path.GetFullPath(_prototypesDir)))
                {
                    res.StatusCode = 403;
                    await WriteJsonAsync(res, new { error = "Access denied" });
                    break;
                }
                if (!File.Exists(oldFull))
                {
                    res.StatusCode = 404;
                    await WriteJsonAsync(res, new { error = "File not found" });
                    break;
                }
                File.Move(oldFull, newFull);
                await WriteJsonAsync(res, new { success = true, newPath = Path.GetRelativePath(_prototypesDir, newFull).Replace('\\', '/') });
                break;
            }

            case "/api/delete-file":
            {
                var relPath = req.QueryString["path"];
                if (string.IsNullOrEmpty(relPath))
                {
                    res.StatusCode = 400;
                    await WriteJsonAsync(res, new { error = "Missing path" });
                    break;
                }
                var fullPath = Path.GetFullPath(Path.Combine(_prototypesDir, relPath));
                if (!fullPath.StartsWith(Path.GetFullPath(_prototypesDir)))
                {
                    res.StatusCode = 403;
                    await WriteJsonAsync(res, new { error = "Access denied" });
                    break;
                }
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                await WriteJsonAsync(res, new { success = true });
                break;
            }

            case "/api/create-file":
            {
                using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                var bodyStr = await reader.ReadToEndAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(bodyStr);
                var parentDir = doc.TryGetProperty("dir", out var dirEl) ? dirEl.GetString() ?? "" : "";
                var fileName = doc.GetProperty("name").GetString()!;
                var content = doc.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";

                var dirFull = string.IsNullOrEmpty(parentDir)
                    ? _prototypesDir
                    : Path.GetFullPath(Path.Combine(_prototypesDir, parentDir));
                var fileFull = Path.Combine(dirFull, fileName);

                if (!fileFull.StartsWith(Path.GetFullPath(_prototypesDir)))
                {
                    res.StatusCode = 403;
                    await WriteJsonAsync(res, new { error = "Access denied" });
                    break;
                }
                Directory.CreateDirectory(dirFull);
                await File.WriteAllTextAsync(fileFull, content, Encoding.UTF8);
                var rel = Path.GetRelativePath(_prototypesDir, fileFull).Replace('\\', '/');
                RefreshIndexForFile(fileFull, rel);
                await WriteJsonAsync(res, new { success = true, path = rel });
                break;
            }

            case "/api/file-stamps":
            {
                // Return last-modified timestamps for requested files
                using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                var bodyStr = await reader.ReadToEndAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(bodyStr);
                var paths = doc.GetProperty("paths").EnumerateArray()
                    .Select(p => p.GetString()!).ToList();
                var stamps = new Dictionary<string, long>();
                foreach (var rp in paths)
                {
                    var fp = Path.GetFullPath(Path.Combine(_prototypesDir, rp));
                    if (fp.StartsWith(Path.GetFullPath(_prototypesDir)) && File.Exists(fp))
                        stamps[rp] = File.GetLastWriteTimeUtc(fp).Ticks;
                    else
                        stamps[rp] = -1;
                }
                await WriteJsonAsync(res, stamps);
                break;
            }

            default:
                res.StatusCode = 404;
                await WriteJsonAsync(res, new { error = "Unknown API endpoint" });
                break;
        }
    }

    private static async Task HandleFileEndpointAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        if (string.IsNullOrEmpty(relPath))
        {
            res.StatusCode = 400;
            await WriteJsonAsync(res, new { error = "Missing 'path' query parameter" });
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_prototypesDir, relPath));
        if (!fullPath.StartsWith(Path.GetFullPath(_prototypesDir)))
        {
            res.StatusCode = 403;
            await WriteJsonAsync(res, new { error = "Access denied" });
            return;
        }

        if (req.HttpMethod == "GET")
        {
            if (!File.Exists(fullPath))
            {
                res.StatusCode = 404;
                await WriteJsonAsync(res, new { error = "File not found" });
                return;
            }
            var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
            await WriteJsonAsync(res, new { content, path = relPath });
        }
        else if (req.HttpMethod == "POST")
        {
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(body);

            if (!doc.TryGetProperty("content", out var contentEl))
            {
                res.StatusCode = 400;
                await WriteJsonAsync(res, new { error = "Missing 'content' in body" });
                return;
            }

            var content = contentEl.GetString()!;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);

            RefreshIndexForFile(fullPath, relPath);

            await WriteJsonAsync(res, new { success = true });
        }
    }

    private static async Task ServeMetadataAsync(HttpListenerResponse res)
    {
        var metaPath = Path.Combine(_redactorDir, "metadata.json");
        if (!File.Exists(metaPath))
        {
            res.StatusCode = 404;
            await WriteJsonAsync(res, new { error = "metadata.json not found. Build the project first." });
            return;
        }
        var bytes = await File.ReadAllBytesAsync(metaPath);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private static async Task ServeStaticAsync(string urlPath, HttpListenerResponse res)
    {
        if (urlPath == "/") urlPath = "/index.html";

        var filePath = Path.Combine(_redactorDir, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(filePath))
        {
            res.StatusCode = 404;
            var msg = Encoding.UTF8.GetBytes("404 Not Found");
            res.ContentType = "text/plain";
            await res.OutputStream.WriteAsync(msg);
            return;
        }

        res.ContentType = MimeType(filePath);
        var content = await File.ReadAllBytesAsync(filePath);
        res.ContentLength64 = content.Length;
        await res.OutputStream.WriteAsync(content);
    }

    private static string MimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream",
    };

    private static List<FileTreeNode> BuildFileTree(string baseDir, string relativePath)
    {
        var fullPath = string.IsNullOrEmpty(relativePath) ? baseDir : Path.Combine(baseDir, relativePath);
        if (!Directory.Exists(fullPath))
            return new();

        var nodes = new List<FileTreeNode>();

        foreach (var dir in Directory.GetDirectories(fullPath).OrderBy(d => d))
        {
            var name = Path.GetFileName(dir);
            var rel = string.IsNullOrEmpty(relativePath) ? name : $"{relativePath}/{name}";
            nodes.Add(new FileTreeNode
            {
                Name = name,
                Path = rel,
                IsDir = true,
                Children = BuildFileTree(baseDir, rel),
            });
        }

        foreach (var file in Directory.GetFiles(fullPath)
                     .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            var rel = string.IsNullOrEmpty(relativePath) ? name : $"{relativePath}/{name}";
            nodes.Add(new FileTreeNode { Name = name, Path = rel, IsDir = false });
        }

        return nodes;
    }

    private static Dictionary<string, List<ProtoIndexEntry>> BuildProtoIndex(string prototypesDir)
    {
        var index = new Dictionary<string, List<ProtoIndexEntry>>();
        if (!Directory.Exists(prototypesDir))
            return index;

        var files = Directory.GetFiles(prototypesDir, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(prototypesDir, "*.yaml", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            try
            {
                var rel = Path.GetRelativePath(prototypesDir, file).Replace('\\', '/');
                ScanYamlFile(file, rel, index);
            }
            catch { /* skip unreadable files */ }
        }

        return index;
    }

    private static void ScanYamlFile(string filePath, string relativePath,
        Dictionary<string, List<ProtoIndexEntry>> index)
    {
        var lines = File.ReadAllLines(filePath);
        string? curType = null, curId = null, curName = null;
        List<string>? curParents = null;
        bool curAbstract = false;
        bool inParentList = false;

        void Flush()
        {
            if (curType == null || curId == null) return;
            if (!index.ContainsKey(curType))
                index[curType] = new List<ProtoIndexEntry>();

            index[curType].Add(new ProtoIndexEntry
            {
                Id = curId,
                Name = curName,
                File = relativePath,
                Parents = curParents?.ToArray(),
                Abstract = curAbstract,
            });
        }

        foreach (var line in lines)
        {
            var m = Regex.Match(line, @"^- type:\s+(.+)$");
            if (m.Success)
            {
                Flush();
                curType = m.Groups[1].Value.Trim();
                curId = null; curName = null; curParents = null; curAbstract = false; inParentList = false;
                continue;
            }

            if (curType == null) continue;

            m = Regex.Match(line, @"^  id:\s+(.+)$");
            if (m.Success) { curId = m.Groups[1].Value.Trim(); inParentList = false; continue; }

            m = Regex.Match(line, @"^  name:\s+(.+)$");
            if (m.Success) { curName = m.Groups[1].Value.Trim(); inParentList = false; continue; }

            if (line == "  parent:")
            {
                curParents = new List<string>();
                inParentList = true;
                continue;
            }

            m = Regex.Match(line, @"^  parent:\s+(.+)$");
            if (m.Success)
            {
                var val = m.Groups[1].Value.Trim();
                if (val.StartsWith('['))
                {
                    curParents = val.Trim('[', ']').Split(',')
                        .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                }
                else
                {
                    curParents = new List<string> { val };
                }
                inParentList = false;
                continue;
            }

            if (inParentList)
            {
                m = Regex.Match(line, @"^  - (.+)$");
                if (m.Success)
                {
                    curParents?.Add(m.Groups[1].Value.Trim());
                    continue;
                }
                inParentList = false;
            }

            m = Regex.Match(line, @"^  abstract:\s+(true|false)$");
            if (m.Success) { curAbstract = m.Groups[1].Value == "true"; continue; }
        }

        Flush();
    }

    private static void RefreshIndexForFile(string fullPath, string relativePath)
    {
        foreach (var list in _protoIndex.Values)
            list.RemoveAll(e => e.File == relativePath);

        try { ScanYamlFile(fullPath, relativePath, _protoIndex); }
        catch { /* ignore */ }
    }

    private static List<ProtoSearchResult> SearchProtos(string type, string query, int limit)
    {
        if (!_protoIndex.TryGetValue(type, out var entries))
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

    private static async Task WriteJsonAsync(HttpListenerResponse res, object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { /* non-critical */ }
    }
}

public sealed class FileTreeNode
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDir { get; set; }
    public List<FileTreeNode>? Children { get; set; }
}

public sealed class ProtoIndexEntry
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string File { get; set; } = "";
    public string[]? Parents { get; set; }
    public bool Abstract { get; set; }
}

public sealed class ProtoSearchResult
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
}
