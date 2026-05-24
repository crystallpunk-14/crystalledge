using System;
using System.IO;
using System.Linq;

namespace Content.Redactor.Redactor;

/// <summary>
/// Locates a <c>.cs</c> source file for a fully-qualified type name by scanning
/// Content.* and RobustToolbox directories. Best-effort: returns the first match.
/// </summary>
internal sealed class SourceLocator
{
    private readonly string _solutionRoot;

    public SourceLocator(string solutionRoot) => _solutionRoot = solutionRoot;

    public string? Find(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return null;

        var shortName = className.Contains('.')
            ? className[(className.LastIndexOf('.') + 1)..]
            : className;
        if (shortName.Contains('+'))
            shortName = shortName[..shortName.IndexOf('+')];

        var fileName = shortName + ".cs";
        var searchDirs = new[] { "Content.Server", "Content.Client", "Content.Shared", "RobustToolbox" };

        foreach (var dir in searchDirs)
        {
            var fullDir = Path.Combine(_solutionRoot, dir);
            if (!Directory.Exists(fullDir)) continue;
            try
            {
                var files = Directory.GetFiles(fullDir, fileName, SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }
            catch { /* ignore */ }
        }
        return null;
    }
}
