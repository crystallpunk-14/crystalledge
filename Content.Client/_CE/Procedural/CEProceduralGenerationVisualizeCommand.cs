using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client._CE.Procedural;

/// <summary>
/// Toggles the <see cref="CEProceduralGenerationOverlay"/> that visualizes
/// the abstract room graph from <see cref="CEGeneratingProceduralDungeonComponent"/>.
/// </summary>
public sealed class CEProceduralGenerationVisualizeCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override string Command => "dungen_generation_visualize";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlay.HasOverlay<CEProceduralGenerationOverlay>())
        {
            _overlay.RemoveOverlay<CEProceduralGenerationOverlay>();
            shell.WriteLine(Loc.GetString("cmd-ce-dungen-generation-visualize-disabled"));
        }
        else
        {
            _overlay.AddOverlay(new CEProceduralGenerationOverlay());
            shell.WriteLine(Loc.GetString("cmd-ce-dungen-generation-visualize-enabled"));
        }
    }
}
