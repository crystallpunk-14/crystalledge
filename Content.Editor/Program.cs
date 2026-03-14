using Robust.Client;

namespace Content.Editor;

internal static class Program
{
    public static void Main(string[] args)
    {
        ContentStart.StartLibrary(args,
            new GameControllerOptions
        {
            Sandboxing = false,
            ContentModulePrefix = "Content.",
            ContentBuildDirectory = "Content.Editor",
            DefaultWindowTitle = "SS14 Prototype Editor",
            UserDataDirectoryName = "SS14 Editor",
            ConfigFileName = "editor.toml",
        });
    }
}
