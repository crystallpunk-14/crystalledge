using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Content.Redactor.Redactor;

/// <summary>
/// Maps <c>/api/*</c> paths to handler delegates and dispatches incoming
/// requests. Endpoint handlers are deliberately small; heavy lifting lives in
/// dedicated services (<see cref="ProtoIndexService"/>, <see cref="SourceLocator"/>, ...).
/// </summary>
internal sealed class ApiRouter
{
    private readonly RedactorContext _ctx;
    private readonly Dictionary<string, Func<HttpListenerRequest, HttpListenerResponse, Task>> _routes;

    public ApiRouter(RedactorContext ctx)
    {
        _ctx = ctx;
        _routes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/tree"] = HandleTreeAsync,
            ["/api/file"] = HandleFileAsync,
            ["/api/metadata"] = HandleMetadataAsync,
            ["/api/proto-index"] = HandleProtoIndexAsync,
            ["/api/search-protos"] = HandleSearchProtosAsync,
            ["/api/refresh-index"] = HandleRefreshIndexAsync,
            ["/api/open-in-explorer"] = HandleOpenInExplorerAsync,
            ["/api/open-default"] = HandleOpenDefaultAsync,
            ["/api/open-source"] = HandleOpenSourceAsync,
            ["/api/rename-file"] = HandleRenameFileAsync,
            ["/api/delete-file"] = HandleDeleteFileAsync,
            ["/api/create-file"] = HandleCreateFileAsync,
            ["/api/file-stamps"] = HandleFileStampsAsync,
            ["/api/rename-proto-id"] = HandleRenameProtoIdAsync,
            ["/api/create-folder"] = HandleCreateFolderAsync,
            ["/api/rename-folder"] = HandleRenameFolderAsync,
            ["/api/delete-folder"] = HandleDeleteFolderAsync,
            ["/api/texture"] = HandleTextureAsync,
            ["/api/texture-browse"] = HandleTextureBrowseAsync,
            ["/api/events"] = HandleEventsAsync,
        };
    }

    public async Task<bool> DispatchAsync(string path, HttpListenerRequest req, HttpListenerResponse res)
    {
        res.ContentType = "application/json; charset=utf-8";
        if (_routes.TryGetValue(path, out var handler))
        {
            await handler(req, res);
            // The events endpoint hijacks the response for the lifetime of the
            // connection; tell the caller not to close it.
            return path.Equals("/api/events", StringComparison.OrdinalIgnoreCase);
        }
        Console.Error.WriteLine($"[Redactor] Unknown API endpoint: {path}");
        await HttpJson.WriteErrorAsync(res, 404, "Unknown API endpoint");
        return false;
    }

    // ---------------------------------------------------------------------
    // Tree & metadata
    // ---------------------------------------------------------------------

    private Task HandleTreeAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var tree = FileTreeService.Build(_ctx.PrototypesDir);
        if (Directory.Exists(_ctx.EnginePrototypesDir))
        {
            var engineTree = FileTreeService.Build(_ctx.EnginePrototypesDir, "", ProtoIndexService.EnginePrefix);
            FileTreeService.MarkReadOnly(engineTree);
            tree.Add(new FileTreeNode
            {
                Name = "⚙ Engine (read-only)",
                Path = "__engine__",
                IsDir = true,
                ReadOnly = true,
                Children = engineTree,
            });
        }
        return HttpJson.WriteAsync(res, tree);
    }

    private async Task HandleMetadataAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var metaPath = Path.Combine(_ctx.RedactorDir, "metadata.json");
        if (!File.Exists(metaPath))
        {
            await HttpJson.WriteErrorAsync(res, 404, "metadata.json not found. Build the project first.");
            return;
        }
        var bytes = await File.ReadAllBytesAsync(metaPath);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private Task HandleProtoIndexAsync(HttpListenerRequest req, HttpListenerResponse res)
        => HttpJson.WriteAsync(res, _ctx.ProtoIndex.Index);

    private Task HandleSearchProtosAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var q = req.QueryString["q"] ?? "";
        var type = req.QueryString["type"] ?? "entity";
        var limit = int.TryParse(req.QueryString["limit"], out var l) ? l : 50;
        return HttpJson.WriteAsync(res, _ctx.ProtoIndex.Search(type, q, limit));
    }

    private Task HandleRefreshIndexAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        _ctx.ProtoIndex.Rebuild();
        return HttpJson.WriteAsync(res, new { count = _ctx.ProtoIndex.TotalCount });
    }

    // ---------------------------------------------------------------------
    // File operations
    // ---------------------------------------------------------------------

    private async Task HandleFileAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        if (string.IsNullOrEmpty(relPath))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Missing 'path' query parameter");
            return;
        }

        bool isEngine = relPath.StartsWith(ProtoIndexService.EnginePrefix);
        var baseDir = isEngine ? _ctx.EnginePrototypesDir : _ctx.PrototypesDir;
        var actualRel = isEngine ? relPath[ProtoIndexService.EnginePrefix.Length..] : relPath;

        var fullPath = PathSecurity.Resolve(baseDir, actualRel);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }

        if (req.HttpMethod == "GET")
        {
            if (!File.Exists(fullPath))
            {
                await HttpJson.WriteErrorAsync(res, 404, "File not found");
                return;
            }
            var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
            await HttpJson.WriteAsync(res, new { content, path = relPath, readOnly = isEngine });
        }
        else if (req.HttpMethod == "POST")
        {
            if (isEngine)
            {
                await HttpJson.WriteErrorAsync(res, 403, "Engine prototypes are read-only");
                return;
            }
            var doc = await HttpJson.ReadBodyAsync(req);
            if (!doc.TryGetProperty("content", out var contentEl))
            {
                await HttpJson.WriteErrorAsync(res, 400, "Missing 'content' in body");
                return;
            }
            var content = contentEl.GetString()!;
            content = content.Replace("\r\n", "\n").Replace("\r", "\n");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            _ctx.FileWatcher.SuppressNext(fullPath);
            await File.WriteAllTextAsync(fullPath, content, new UTF8Encoding(false));
            _ctx.ProtoIndex.RefreshFile(fullPath, relPath);
            await HttpJson.WriteAsync(res, new { success = true });
        }
    }

    private async Task HandleRenameFileAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var oldRel = doc.GetProperty("oldPath").GetString()!;
        var newName = doc.GetProperty("newName").GetString()!;

        var oldFull = PathSecurity.Resolve(_ctx.PrototypesDir, oldRel);
        if (oldFull == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        var newFull = Path.Combine(Path.GetDirectoryName(oldFull)!, newName);
        if (PathSecurity.Resolve(_ctx.PrototypesDir, Path.GetRelativePath(_ctx.PrototypesDir, newFull)) == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!File.Exists(oldFull))
        {
            await HttpJson.WriteErrorAsync(res, 404, "File not found");
            return;
        }
        File.Move(oldFull, newFull);
        Console.WriteLine($"[Redactor] File renamed: {oldRel} -> {newName}");
        var newRel = Path.GetRelativePath(_ctx.PrototypesDir, newFull).Replace('\\', '/');
        await HttpJson.WriteAsync(res, new { success = true, newPath = newRel });
    }

    private async Task HandleDeleteFileAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        if (string.IsNullOrEmpty(relPath))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Missing path");
            return;
        }
        var fullPath = PathSecurity.Resolve(_ctx.PrototypesDir, relPath);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            Console.WriteLine($"[Redactor] File deleted: {relPath}");
        }
        await HttpJson.WriteAsync(res, new { success = true });
    }

    private async Task HandleCreateFileAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var parentDir = doc.TryGetProperty("dir", out var dirEl) ? dirEl.GetString() ?? "" : "";
        var fileName = doc.GetProperty("name").GetString()!;
        var content = doc.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";

        var dirFull = string.IsNullOrEmpty(parentDir)
            ? Path.GetFullPath(_ctx.PrototypesDir)
            : PathSecurity.Resolve(_ctx.PrototypesDir, parentDir);
        if (dirFull == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        var fileFull = Path.Combine(dirFull, fileName);
        if (PathSecurity.Resolve(_ctx.PrototypesDir, Path.GetRelativePath(_ctx.PrototypesDir, fileFull)) == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        Directory.CreateDirectory(dirFull);
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");
        _ctx.FileWatcher.SuppressNext(fileFull);
        await File.WriteAllTextAsync(fileFull, content, new UTF8Encoding(false));
        var rel = Path.GetRelativePath(_ctx.PrototypesDir, fileFull).Replace('\\', '/');
        _ctx.ProtoIndex.RefreshFile(fileFull, rel);
        await HttpJson.WriteAsync(res, new { success = true, path = rel });
    }

    private async Task HandleFileStampsAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var paths = doc.GetProperty("paths").EnumerateArray().Select(p => p.GetString()!).ToList();
        var stamps = new Dictionary<string, long>();
        foreach (var rp in paths)
        {
            var fp = PathSecurity.Resolve(_ctx.PrototypesDir, rp);
            stamps[rp] = (fp != null && File.Exists(fp))
                ? File.GetLastWriteTimeUtc(fp).Ticks
                : -1;
        }
        await HttpJson.WriteAsync(res, stamps);
    }

    private async Task HandleRenameProtoIdAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var filePath = doc.GetProperty("path").GetString()!;
        var oldId = doc.GetProperty("oldId").GetString()!;
        var newId = doc.GetProperty("newId").GetString()!;
        var protoType = doc.GetProperty("type").GetString()!;

        var fullPath = PathSecurity.Resolve(_ctx.PrototypesDir, filePath);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!File.Exists(fullPath))
        {
            await HttpJson.WriteErrorAsync(res, 404, "File not found");
            return;
        }

        _ctx.ProtoIndex.RefreshFile(fullPath, filePath);
        Console.WriteLine($"[Redactor] Renamed prototype ID: {protoType}/{oldId} -> {newId} in {filePath}");
        await HttpJson.WriteAsync(res, new { success = true });
    }

    // ---------------------------------------------------------------------
    // Folder CRUD
    // ---------------------------------------------------------------------

    private async Task HandleCreateFolderAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var parentDir = doc.TryGetProperty("dir", out var dirEl) ? dirEl.GetString() ?? "" : "";
        var name = doc.GetProperty("name").GetString()!;

        if (!IsValidLeafName(name))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Invalid folder name");
            return;
        }

        var baseDir = string.IsNullOrEmpty(parentDir)
            ? Path.GetFullPath(_ctx.PrototypesDir)
            : PathSecurity.Resolve(_ctx.PrototypesDir, parentDir);
        if (baseDir == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        var target = Path.Combine(baseDir, name);
        if (PathSecurity.Resolve(_ctx.PrototypesDir, Path.GetRelativePath(_ctx.PrototypesDir, target)) == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (Directory.Exists(target))
        {
            await HttpJson.WriteErrorAsync(res, 409, "Folder already exists");
            return;
        }
        Directory.CreateDirectory(target);
        var rel = Path.GetRelativePath(_ctx.PrototypesDir, target).Replace('\\', '/');
        Console.WriteLine($"[Redactor] Folder created: {rel}");
        await HttpJson.WriteAsync(res, new { success = true, path = rel });
    }

    private async Task HandleRenameFolderAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var doc = await HttpJson.ReadBodyAsync(req);
        var oldRel = doc.GetProperty("oldPath").GetString()!;
        var newName = doc.GetProperty("newName").GetString()!;

        if (!IsValidLeafName(newName))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Invalid folder name");
            return;
        }

        var oldFull = PathSecurity.Resolve(_ctx.PrototypesDir, oldRel);
        if (oldFull == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!Directory.Exists(oldFull))
        {
            await HttpJson.WriteErrorAsync(res, 404, "Folder not found");
            return;
        }
        var newFull = Path.Combine(Path.GetDirectoryName(oldFull)!, newName);
        if (PathSecurity.Resolve(_ctx.PrototypesDir, Path.GetRelativePath(_ctx.PrototypesDir, newFull)) == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (Directory.Exists(newFull) || File.Exists(newFull))
        {
            await HttpJson.WriteErrorAsync(res, 409, "Target already exists");
            return;
        }
        Directory.Move(oldFull, newFull);
        // Rebuild affected entries: any indexed file under oldRel must now be remapped.
        _ctx.ProtoIndex.Rebuild();
        var newRel = Path.GetRelativePath(_ctx.PrototypesDir, newFull).Replace('\\', '/');
        Console.WriteLine($"[Redactor] Folder renamed: {oldRel} -> {newRel}");
        await HttpJson.WriteAsync(res, new { success = true, newPath = newRel });
    }

    private async Task HandleDeleteFolderAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        // Accept either query (?path=) or JSON body { path, recursive }.
        string? relPath = req.QueryString["path"];
        bool recursive = false;
        if (req.HttpMethod == "POST")
        {
            var doc = await HttpJson.ReadBodyAsync(req);
            if (doc.TryGetProperty("path", out var p)) relPath = p.GetString();
            if (doc.TryGetProperty("recursive", out var r)) recursive = r.GetBoolean();
        }

        if (string.IsNullOrEmpty(relPath))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Missing path");
            return;
        }

        var fullPath = PathSecurity.Resolve(_ctx.PrototypesDir, relPath);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!Directory.Exists(fullPath))
        {
            await HttpJson.WriteAsync(res, new { success = true });
            return;
        }
        // Refuse to delete a non-empty folder unless recursive=true is set.
        var hasContents = Directory.EnumerateFileSystemEntries(fullPath).Any();
        if (hasContents && !recursive)
        {
            await HttpJson.WriteErrorAsync(res, 409, "Folder not empty");
            return;
        }
        Directory.Delete(fullPath, recursive);
        _ctx.ProtoIndex.Rebuild();
        Console.WriteLine($"[Redactor] Folder deleted: {relPath} (recursive={recursive})");
        await HttpJson.WriteAsync(res, new { success = true });
    }

    /// <summary>
    /// Validates a single path segment supplied as a folder or file name. Rejects
    /// empty strings, path separators, parent-directory references, and Windows
    /// reserved characters.
    /// </summary>
    private static bool IsValidLeafName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name == "." || name == "..") return false;
        if (name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0) return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        return true;
    }

    // ---------------------------------------------------------------------
    // OS integrations
    // ---------------------------------------------------------------------

    private async Task HandleOpenInExplorerAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        var fullPath = PathSecurity.Resolve(_ctx.PrototypesDir, relPath);
        if (fullPath != null)
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
        await HttpJson.WriteAsync(res, new { success = true });
    }

    private async Task HandleOpenDefaultAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        var fullPath = PathSecurity.Resolve(_ctx.PrototypesDir, relPath);
        if (fullPath != null && File.Exists(fullPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
            }
            catch { /* non-critical */ }
        }
        await HttpJson.WriteAsync(res, new { success = true });
    }

    private async Task HandleOpenSourceAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var className = req.QueryString["class"];
        if (!string.IsNullOrEmpty(className))
        {
            var found = _ctx.SourceLocator.Find(className);
            if (found != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = found, UseShellExecute = true });
                }
                catch { /* non-critical */ }
                await HttpJson.WriteAsync(res, new { success = true, path = found });
                return;
            }
        }
        await HttpJson.WriteAsync(res, new { success = false, error = "Source file not found" });
    }

    // ---------------------------------------------------------------------
    // Textures
    // ---------------------------------------------------------------------

    private async Task HandleTextureAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = req.QueryString["path"];
        if (string.IsNullOrEmpty(relPath))
        {
            await HttpJson.WriteErrorAsync(res, 400, "Missing 'path' query parameter");
            return;
        }
        relPath = relPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        var fullPath = PathSecurity.Resolve(_ctx.TexturesDir, relPath);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!File.Exists(fullPath))
        {
            await HttpJson.WriteErrorAsync(res, 404, "File not found");
            return;
        }

        res.ContentType = StaticMime.For(fullPath);
        res.AddHeader("Cache-Control", "public, max-age=300");
        var bytes = await File.ReadAllBytesAsync(fullPath);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
    }

    private async Task HandleTextureBrowseAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        var relPath = (req.QueryString["path"] ?? "")
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = PathSecurity.Resolve(_ctx.TexturesDir, relPath.Length == 0 ? "." : relPath);
        if (fullPath == null)
        {
            await HttpJson.WriteErrorAsync(res, 403, "Access denied");
            return;
        }
        if (!Directory.Exists(fullPath))
        {
            await HttpJson.WriteAsync(res, new { dirs = Array.Empty<string>(), files = Array.Empty<string>() });
            return;
        }
        var dirs = Directory.GetDirectories(fullPath).Select(Path.GetFileName).OrderBy(n => n).ToList();
        var files = Directory.GetFiles(fullPath).Select(Path.GetFileName)
            .Where(n => n != null && !n.StartsWith('.'))
            .OrderBy(n => n).ToList();
        await HttpJson.WriteAsync(res, new { dirs, files });
    }

    // ---------------------------------------------------------------------
    // Event stream (SSE)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Long-lived <c>text/event-stream</c> endpoint. Replaces client-side polling
    /// for file change detection. Connection stays open until the client closes
    /// it; the response is not auto-closed by the dispatcher.
    /// </summary>
    private Task HandleEventsAsync(HttpListenerRequest req, HttpListenerResponse res)
        => _ctx.Events.SubscribeAsync(res, default);
}
