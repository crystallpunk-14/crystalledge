using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Server._CE.MetaChest;

/// <summary>
/// metacoffer_open &lt;nick|userId&gt; [slot=0]
/// Opens a player's meta chest for the admin. Denied if the target currently has the slot open.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class CEMetaChestCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "metacoffer_open";
    public override string Description => "Open another player's meta chest as admin.";
    public override string Help => "Usage: metacoffer_open <nick|userId> [slot=0]";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } adminMob)
        {
            shell.WriteError("You must be in-game to use this command.");
            return;
        }

        int chestSlot = 0;
        if (args.Length >= 2 && !int.TryParse(args[1], out chestSlot))
        {
            shell.WriteError($"Invalid slot number: '{args[1]}'");
            return;
        }

        var data = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (data == null)
        {
            shell.WriteError($"Player not found: '{args[0]}'");
            return;
        }

        var system = _entities.EntitySysManager.GetEntitySystem<CEMetaChestSystem>();
        if (!system.TryOpenAdminMetaChest(adminMob, shell.Player, (Guid) data.UserId, chestSlot))
        {
            shell.WriteError($"Player '{data.Username}' currently has chest slot {chestSlot} open. Close it first.");
            return;
        }

        shell.WriteLine($"Opened meta chest slot {chestSlot} for '{data.Username}'.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("<nick|userId>"),
            2 => CompletionResult.FromHint("[slot=0]"),
            _ => CompletionResult.Empty,
        };
    }
}
