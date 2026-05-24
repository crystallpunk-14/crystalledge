using System.IO;

namespace Content.Redactor.Redactor;

/// <summary>
/// Helpers that resolve user-supplied relative paths against a trusted base
/// directory while guarding against path-traversal escape attempts.
/// </summary>
internal static class PathSecurity
{
    /// <summary>
    /// Resolves <paramref name="relative"/> against <paramref name="baseDir"/> and
    /// returns the absolute path. Returns <c>null</c> if the resolved path would
    /// escape <paramref name="baseDir"/>.
    /// </summary>
    public static string? Resolve(string baseDir, string? relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        var baseFull = Path.GetFullPath(baseDir);
        var combined = Path.GetFullPath(Path.Combine(baseFull, relative));
        // Ensure trailing separator on base for prefix comparison correctness.
        var basePrefix = baseFull.EndsWith(Path.DirectorySeparatorChar)
            ? baseFull
            : baseFull + Path.DirectorySeparatorChar;
        if (combined != baseFull && !combined.StartsWith(basePrefix))
            return null;
        return combined;
    }
}
