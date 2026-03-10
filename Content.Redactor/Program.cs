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
}
