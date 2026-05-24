using System.IO;

namespace Content.Redactor.Redactor;

/// <summary>
/// Shared runtime context passed to <see cref="ApiRouter"/> and its services.
/// </summary>
internal sealed class RedactorContext
{
    public required string SolutionRoot { get; init; }
    public required string RedactorDir { get; init; }
    public required string PrototypesDir { get; init; }
    public required string TexturesDir { get; init; }
    public required string AudioDir { get; init; }
    public required string EnginePrototypesDir { get; init; }
    public required ProtoIndexService ProtoIndex { get; init; }
    public required SourceLocator SourceLocator { get; init; }
    public required EventStreamService Events { get; init; }
    public required FileWatcherService FileWatcher { get; init; }
}

/// <summary>
/// File extension → MIME type lookup for static asset serving.
/// </summary>
internal static class StaticMime
{
    public static string For(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        ".ogg" => "audio/ogg",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        _ => "application/octet-stream",
    };
}
