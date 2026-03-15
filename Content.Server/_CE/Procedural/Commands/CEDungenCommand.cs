using Content.Server._CE.Procedural.Prototypes;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class CEDungenCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "dungen";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("cmd-dungen-error-args"));
            return;
        }

        var protoId = new ProtoId<CEDungeonLevelPrototype>(args[0]);

        if (!_proto.TryIndex(protoId, out var proto))
        {
            shell.WriteError(Loc.GetString("cmd-dungen-error-unknown-level", ("level", args[0])));
            return;
        }

        var dungeonSystem = _entities.System<CEDungeonSystem>();
        var result = dungeonSystem.GenerateLevel(proto);

        if (!result.Success)
        {
            shell.WriteError(Loc.GetString("cmd-dungen-error-failed", ("level", args[0])));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-dungen-success",
            ("level", args[0]),
            ("mapId", result.MapId?.ToString() ?? "?")));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<CEDungeonLevelPrototype>(proto: _proto),
                Loc.GetString("cmd-dungen-hint-level"));
        }

        return CompletionResult.Empty;
    }
}
