using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Procedural;

/// <summary>
/// Toggles the <see cref="CEDungeonAtlasOverlay"/> that visualizes <see cref="CEDungeonRoom3DPrototype"/>
/// rects for a given <see cref="CEZLevelMapPrototype"/>.
/// </summary>
public sealed class CEDungeonAtlasVisualizeCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "dungen_atlas_visualize";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("cmd-ce-dungen-atlas-visualize-error-args"));
            return;
        }

        // No args or "null" — toggle off.
        if (args.Length == 0 || args[0].Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            if (_overlay.HasOverlay<CEDungeonAtlasOverlay>())
            {
                _overlay.RemoveOverlay<CEDungeonAtlasOverlay>();
                shell.WriteLine(Loc.GetString("cmd-ce-dungen-atlas-visualize-disabled"));
            }
            else
            {
                shell.WriteLine(Loc.GetString("cmd-ce-dungen-atlas-visualize-already-disabled"));
            }
            return;
        }

        var protoId = args[0];
        if (!_proto.HasIndex<CEZLevelMapPrototype>(protoId))
        {
            shell.WriteError(Loc.GetString("cmd-ce-dungen-atlas-visualize-error-unknown", ("id", protoId)));
            return;
        }

        // Get or create overlay, then update its zMapProtoId.
        if (!_overlay.TryGetOverlay<CEDungeonAtlasOverlay>(out var existing))
        {
            existing = new CEDungeonAtlasOverlay();
            _overlay.AddOverlay(existing);
        }

        existing.ZMapProtoId = protoId;
        shell.WriteLine(Loc.GetString("cmd-ce-dungen-atlas-visualize-enabled", ("id", protoId)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<CEZLevelMapPrototype>(proto: _proto),
                Loc.GetString("cmd-ce-dungen-atlas-visualize-hint-zmap"));
        }

        return CompletionResult.Empty;
    }
}
