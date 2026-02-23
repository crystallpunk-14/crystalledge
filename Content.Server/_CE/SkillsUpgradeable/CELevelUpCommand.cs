using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._CE.SkillsUpgradeable;

[AdminCommand(AdminFlags.Fun)]
public sealed class CELevelUpCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override string Command => "levelup";
    public override string Description => "Triggers a skill upgrade selection for the target player.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            // Get completion for players with CESkillUpgradeableComponent
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent>();

            while (query.MoveNext(out var uid, out _))
            {
                // Check if this entity has an attached player session
                if (_playerManager.TryGetSessionByEntity(uid, out var session))
                {
                    options.Add(new CompletionOption(session.Name));
                }
            }

            return CompletionResult.FromHintOptions(options, "Player name");
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
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

        if (!_entities.TryGetComponent<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent>(entity, out var upgradeComp))
        {
            shell.WriteError("Target player does not have skill upgrade component.");
            return;
        }

        var system = _entities.System<CESkillUpgradeableSystem>();
        system.TriggerLevelUp((entity, upgradeComp));

        shell.WriteLine($"Triggered level up for {player.Name}");
    }
}
