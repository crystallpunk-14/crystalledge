using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CE.WorldGen.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed partial class CEWorldRegenCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "dungen_world_regen";
    public override string Description => "Regenerates the procedural world in-place from current prototypes (same seed, z-network preserved).";
    public override string Help => "Usage: dungen_world_regen";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _entities.System<CEWorldGenSystem>().RegenerateWorld();
        shell.WriteLine("World regenerated. Chunks will reload on the next tick.");
    }
}
