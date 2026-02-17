using System.Linq;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Commands;
using Content.Shared.Administration;
using Content.Shared._CE.Achievements.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Achievements;

[AdminCommand(AdminFlags.Debug)]
public sealed class CEAddAchievementCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    public override string Command => "achievementadd";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _player.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(options, "<Player>");
        }

        if (args.Length == 2)
        {
            var all = CompletionHelper.PrototypeIDs<CEAchievementPrototype>(true, _proto);
            return CompletionResult.FromHintOptions(all, "<AchievementId>");
        }

        return CompletionResult.Empty;
    }

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var playerArg = args[0];
        var protoArg = args[1];

        var performer = shell.Player ?? _player.Sessions.FirstOrDefault();
        if (performer == null)
        {
            shell.WriteError("No performer session available.");
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(playerArg);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistadd-not-found", ("username", playerArg)));
            return;
        }

        var sessionUserId = located.UserId;

        if (!_proto.Resolve<CEAchievementPrototype>(protoArg, out var indexedAchievement))
        {
            shell.WriteError($"No such achievement prototype: {protoArg}");
            return;
        }

        try
        {
            var has = await _db.HasPlayerAchievement(sessionUserId, protoArg);
            if (has)
            {
                shell.WriteLine($"Player {located.Username} already has achievement {protoArg}.");
                return;
            }

            await _db.AddPlayerAchievement(sessionUserId, protoArg);
            shell.WriteLine($"Added achievement {protoArg} to player {located.Username}.");
        }
        catch (Exception e)
        {
            shell.WriteError($"Failed to add achievement: {e.Message}");
        }
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class CERemoveAchievementCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    public override string Command => "achievementremove";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _player.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(options, "<Player>");
        }

        if (args.Length == 2)
        {
            var all = CompletionHelper.PrototypeIDs<CEAchievementPrototype>(true, _proto);
            return CompletionResult.FromHintOptions(all, "<AchievementId>");
        }

        return CompletionResult.Empty;
    }

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var playerArg = args[0];
        var protoArg = args[1];

        var performer = shell.Player ?? _player.Sessions.FirstOrDefault();
        if (performer == null)
        {
            shell.WriteError("No performer session available.");
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(playerArg);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistadd-not-found", ("username", playerArg)));
            return;
        }

        var sessionUserId = located.UserId;

        if (!_proto.Resolve<CEAchievementPrototype>(protoArg, out var indexedAchievement))
        {
            shell.WriteError($"No such achievement prototype: {protoArg}");
            return;
        }

        try
        {
            var has = await _db.HasPlayerAchievement(sessionUserId, protoArg);
            if (!has)
            {
                shell.WriteLine($"Player {located.Username} does not have achievement {protoArg}.");
                return;
            }

            var removed = await _db.RemovePlayerAchievement(sessionUserId, protoArg);
            if (removed)
                shell.WriteLine($"Removed achievement {protoArg} from player {located.Username}.");
            else
                shell.WriteError($"Failed to remove achievement {protoArg} from player {located.Username}.");
        }
        catch (Exception e)
        {
            shell.WriteError($"Failed to remove achievement: {e.Message}");
        }
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class CEListAchievementsCommand : LocalizedCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override string Command => "achievementlist";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var options = _player.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
        return CompletionResult.FromHintOptions(options, "<Player>");
    }

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Loc.GetString("shell-need-minimum-one-argument"));
            shell.WriteLine(Help);
            return;
        }

        var name = args[0];
        var data = await _locator.LookupIdByNameOrIdAsync(name);

        if (data is null)
        {
            shell.WriteError(Loc.GetString("cmd-whitelistadd-not-found", ("username", args[0])));
            return;
        }

        var guid = data.UserId;

        try
        {
            var owned = await _db.GetPlayerAchievements(guid);
            if (owned.Count == 0)
            {
                shell.WriteLine($"Player {data.Username} has no achievements.");
                return;
            }

            shell.WriteLine($"Achievements for {data.Username}: {owned.Count}");
            foreach (var id in owned)
            {
                shell.WriteLine(" - " + id);
            }
        }
        catch (Exception e)
        {
            shell.WriteError($"Failed to list achievements: {e.Message}");
        }
    }
}
