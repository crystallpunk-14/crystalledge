using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Animation.Core;

[AdminCommand(AdminFlags.Fun)]
public sealed class CEPlayAnimationCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "playanimation";
    public override string Description => "Plays a animation on a player. Usage: playanimation <Player> <AnimationId>";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _playerManager.Sessions.OrderBy(s => s.Name).Select(s => s.Name).ToArray();
            return CompletionResult.FromHintOptions(options, "Player");
        }
        if (args.Length == 2)
        {
            var options = CompletionHelper.PrototypeIDs<CEAnimationActionPrototype>(true, _proto);
            return CompletionResult.FromHintOptions(options, "AnimationId");
        }
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!_playerManager.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        if (player.AttachedEntity is not { } entity)
        {
            shell.WriteError("Target player has no attached entity.");
            return;
        }

        if (!_proto.Resolve<CEAnimationActionPrototype>(args[1], out var animation))
        {
            shell.WriteError($"Can't resolve animation '{args[1]}'.");
            return;
        }

        var sys = _entities.System<CEAnimationActionSystem>();
        if (!sys.TryPlayAnimation(entity, animation))
        {
            shell.WriteError($"Failed to play animation '{args[1]}' for {player.Name}.");
            return;
        }

        shell.WriteLine($"Played animation '{args[1]}' for {player.Name}.");
    }
}
