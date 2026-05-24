using System;
using System.IO;
using System.Threading.Tasks;
using Content.Redactor.Redactor;

namespace Content.Redactor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "extract":
                var extractRoot = args.Length > 1 ? args[1] : FindSolutionRoot();
                if (extractRoot == null)
                {
                    Console.Error.WriteLine("Could not find solution root. Pass it as argument.");
                    return;
                }
                MetadataExtractor.Extract(extractRoot);
                break;

            case "serve":
                var serveRoot = args.Length > 1 ? args[1] : FindSolutionRoot();
                if (serveRoot == null)
                {
                    Console.Error.WriteLine("Could not find solution root.");
                    return;
                }
                var port = args.Length > 2 ? int.Parse(args[2]) : 5555;

                // Auto-(re)extract metadata if the running binary is newer than the cached
                // metadata.json (or if the cache is missing).  This removes the foot-gun
                // of "I changed the extractor and forgot to re-run extract".
                EnsureMetadataFresh(serveRoot);

                await RedactorServer.StartAsync(serveRoot, port);
                break;

            default:
                PrintUsage();
                break;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SS14 Prototype Redactor");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Content.Redactor extract [solutionRoot]  - Extract prototype metadata to Redactor/metadata.json");
        Console.WriteLine("  Content.Redactor serve [solutionRoot] [port] - Start the visual editor (default port: 5555)");
    }

    private static string? FindSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "SpaceStation14.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    /// <summary>
    /// Re-extracts metadata.json if missing or if any scanned binary is newer
    /// than the cache. Keeps the redactor's view in sync with the latest build
    /// without forcing the user to remember the manual extract step.
    /// </summary>
    private static void EnsureMetadataFresh(string solutionRoot)
    {
        try
        {
            var metaPath = Path.Combine(solutionRoot, "Redactor", "metadata.json");
            var metaTime = File.Exists(metaPath) ? File.GetLastWriteTimeUtc(metaPath) : DateTime.MinValue;

            var newest = DateTime.MinValue;
            foreach (var rel in new[] { "bin/Content.Server", "bin/Content.Client" })
            {
                var dir = Path.Combine(solutionRoot, rel);
                if (!Directory.Exists(dir)) continue;
                foreach (var dll in Directory.EnumerateFiles(dir, "Content.*.dll", SearchOption.TopDirectoryOnly))
                {
                    var t = File.GetLastWriteTimeUtc(dll);
                    if (t > newest) newest = t;
                }
            }

            // Also include this redactor assembly itself: changes to
            // FieldExtractor / MetadataExtractor classifier logic should
            // trigger a regen even if the game DLLs haven't changed.
            try
            {
                var selfDll = typeof(Program).Assembly.Location;
                if (!string.IsNullOrEmpty(selfDll) && File.Exists(selfDll))
                {
                    var t = File.GetLastWriteTimeUtc(selfDll);
                    if (t > newest) newest = t;
                }
            }
            catch { /* ignore */ }

            if (newest > metaTime)
            {
                Console.WriteLine("[Redactor] metadata.json out of date — regenerating...");
                MetadataExtractor.Extract(solutionRoot);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Redactor] EnsureMetadataFresh failed: {ex.Message}");
        }
    }
}
