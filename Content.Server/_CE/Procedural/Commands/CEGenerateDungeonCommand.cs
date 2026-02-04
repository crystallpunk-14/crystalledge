using Content.Server.Administration;
using Content.Shared._CE.Procedural;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class CEGenerateDungeonCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CEDungeonSystem _dungeon = default!;

    public override string Command => "dungen";
    public override string Description => "Creates a new dungeon z-level network.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        foreach (var dungeon in _proto.EnumeratePrototypes<CEDungeonZonePrototype>())
        {
            options.Add(new CompletionOption(dungeon.ID));
        }

        return CompletionResult.FromHintOptions(options, "CEDungeonZonePrototype");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Expected 1 argument: <CEDungeonZonePrototype>");
            return;
        }

        if (!_proto.Resolve<CEDungeonZonePrototype>(args[0], out var dunProto))
        {
            shell.WriteError($"Unknown CEDungeonZonePrototype: {args[0]}");
            return;
        }

        if (_dungeon.TryGenerateDungeon(dunProto))
            shell.WriteLine($"Successfully generated dungeon: {args[0]}");
        else
            shell.WriteError($"Failed to generate dungeon: {args[0]}");
    }
}
