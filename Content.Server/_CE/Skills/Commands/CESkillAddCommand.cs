using Content.Server._CE.Skills;
using Content.Server.Administration;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CESkillAddCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "skilladd";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-skilladd-error-args"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError(Loc.GetString("cmd-skilladd-error-player", ("player", args[0])));
            return;
        }

        if (session.AttachedEntity is not { } target)
        {
            shell.WriteError(Loc.GetString("cmd-skilladd-error-no-entity", ("player", args[0])));
            return;
        }

        var skillId = new ProtoId<CESkillPrototype>(args[1]);

        if (!_proto.HasIndex(skillId))
        {
            shell.WriteError(Loc.GetString("cmd-skilladd-error-unknown-skill", ("skill", args[1])));
            return;
        }

        var skillSystem = _entities.System<CESkillSystem>();

        if (!skillSystem.TryAddSkill(target, skillId))
        {
            shell.WriteError(Loc.GetString("cmd-skilladd-error-failed", ("player", args[0]), ("skill", args[1])));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-skilladd-success", ("player", args[0]), ("skill", args[1])));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(players: _players),
                Loc.GetString("cmd-skilladd-hint-player"));
        }

        if (args.Length == 2)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIDs<CESkillPrototype>(proto: _proto),
                Loc.GetString("cmd-skilladd-hint-skill"));
        }

        return CompletionResult.Empty;
    }
}
