using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Content.Redactor.Redactor;

/// <summary>
/// Lightweight HTTP server that serves the Redactor web UI and exposes
/// REST endpoints for file browsing, reading/writing YAML, and searching
/// prototype IDs. Routing logic lives in <see cref="ApiRouter"/>; service
/// implementations are split into dedicated classes.
/// </summary>
public static class RedactorServer
{
    public static async Task StartAsync(string solutionRoot, int port)
    {
        var ctx = BuildContext(solutionRoot);

        Console.WriteLine("[Redactor] Building prototype index...");
        ctx.ProtoIndex.Rebuild();
        Console.WriteLine($"[Redactor] Indexed {ctx.ProtoIndex.TotalCount} prototypes across {ctx.ProtoIndex.TypeCount} types");

        // Push external file changes through the event stream and keep the index fresh.
        ctx.FileWatcher.Changed += evt =>
        {
            var rel = evt.RelativePath;
            switch (evt.Kind)
            {
                case FileChangeKind.Deleted:
                    ctx.ProtoIndex.RefreshFile(evt.FullPath, rel);
                    break;
                case FileChangeKind.Created:
                case FileChangeKind.Changed:
                    if (File.Exists(evt.FullPath))
                        ctx.ProtoIndex.RefreshFile(evt.FullPath, rel);
                    break;
            }
            ctx.Events.Broadcast(new { type = "file-change", kind = evt.Kind.ToString().ToLowerInvariant(), path = rel });
        };
        ctx.FileWatcher.Start();

        var router = new ApiRouter(ctx);

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        Console.WriteLine($"[Redactor] Editor running at http://localhost:{port}/");
        Console.WriteLine("[Redactor] Press Ctrl+C to stop.");

        TryOpenBrowser($"http://localhost:{port}/");

        while (true)
        {
            var httpCtx = await listener.GetContextAsync();
            _ = Task.Run(() => HandleRequestAsync(httpCtx, router, ctx));
        }
    }

    private static RedactorContext BuildContext(string solutionRoot)
    {
        var redactorDir = Path.Combine(solutionRoot, "Redactor");
        var prototypesDir = Path.Combine(solutionRoot, "Resources", "Prototypes");
        var texturesDir = Path.Combine(solutionRoot, "Resources", "Textures");
        var enginePrototypesDir = Path.Combine(solutionRoot, "RobustToolbox", "Resources", "EnginePrototypes");

        return new RedactorContext
        {
            SolutionRoot = solutionRoot,
            RedactorDir = redactorDir,
            PrototypesDir = prototypesDir,
            TexturesDir = texturesDir,
            EnginePrototypesDir = enginePrototypesDir,
            ProtoIndex = new ProtoIndexService(prototypesDir, enginePrototypesDir),
            SourceLocator = new SourceLocator(solutionRoot),
            Events = new EventStreamService(),
            FileWatcher = new FileWatcherService(prototypesDir),
        };
    }

    private static async Task HandleRequestAsync(HttpListenerContext httpCtx, ApiRouter router, RedactorContext ctx)
    {
        var req = httpCtx.Request;
        var res = httpCtx.Response;

        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 200;
            res.Close();
            return;
        }

        bool keepAlive = false;
        try
        {
            var path = req.Url?.AbsolutePath ?? "/";
            if (path.StartsWith("/api/"))
            {
                Console.WriteLine($"[Redactor] {req.HttpMethod} {path}");
                keepAlive = await router.DispatchAsync(path, req, res);
            }
            else
            {
                await ServeStaticAsync(path, ctx.RedactorDir, res);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Redactor] ERROR handling {req.HttpMethod} {req.Url}: {ex}");
            res.StatusCode = 500;
            res.ContentType = "application/json";
            await HttpJson.WriteAsync(res, new { error = ex.Message });
        }
        finally
        {
            if (!keepAlive) res.Close();
        }
    }

    private static async Task ServeStaticAsync(string urlPath, string redactorDir, HttpListenerResponse res)
    {
        if (urlPath == "/") urlPath = "/index.html";
        var filePath = Path.Combine(redactorDir, urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        // Ensure the static path stays inside redactorDir.
        if (!filePath.StartsWith(Path.GetFullPath(redactorDir), StringComparison.OrdinalIgnoreCase)
            && !File.Exists(filePath))
        {
            res.StatusCode = 404;
            res.ContentType = "text/plain";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("404 Not Found"));
            return;
        }

        if (!File.Exists(filePath))
        {
            res.StatusCode = 404;
            res.ContentType = "text/plain";
            await res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("404 Not Found"));
            return;
        }

        res.ContentType = StaticMime.For(filePath);
        var content = await File.ReadAllBytesAsync(filePath);
        res.ContentLength64 = content.Length;
        await res.OutputStream.WriteAsync(content);
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
    public bool ReadOnly { get; set; }
    public List<FileTreeNode>? Children { get; set; }
}

public sealed class ProtoIndexEntry
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string File { get; set; } = "";
    public string[]? Parents { get; set; }
    public bool Abstract { get; set; }
    public bool ReadOnly { get; set; }
}

public sealed class ProtoSearchResult
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
}
