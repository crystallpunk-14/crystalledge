using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._CE.Achievements;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._CE.Achievements;

/// <summary>
/// Server-side system that sends cached achievement data to clients on connection.
/// Refreshes achievement percentage statistics at the end of each round.
/// </summary>
public sealed class CEAchievementsSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private Dictionary<string, float> _cachedPercentages = new();
    private bool _initialLoad;

    public override void Initialize()
    {
        base.Initialize();

        _netManager.RegisterNetMessage<CEMsgAchievements>();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private async void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        await RefreshCachedPercentagesAsync();
        await SendAchievementsToAllPlayers();
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            return;

        var userId = args.Session.UserId;

        if (!_initialLoad)
        {
            _initialLoad = true;
            await RefreshCachedPercentagesAsync();
        }

        try
        {
            var playerAchievements = await _db.GetPlayerAchievements(userId);

            var msg = new CEMsgAchievements
            {
                PlayerAchievements = new HashSet<string>(playerAchievements),
                AchievementPercentages = _cachedPercentages,
            };

            _netManager.ServerSendMessage(msg, args.Session.Channel);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to send achievements to {args.Session.Name}: {e}");
        }
    }

    private async void RefreshCachedPercentages()
    {
        await RefreshCachedPercentagesAsync();
    }

    private async Task RefreshCachedPercentagesAsync()
    {
        try
        {
            _cachedPercentages = await _db.GetAchievementPercentages();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to refresh achievement percentages: {e}");
        }
    }

    private async Task SendAchievementsToAllPlayers()
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame &&
                session.Status != SessionStatus.Connected)
                continue;

            try
            {
                var playerAchievements = await _db.GetPlayerAchievements(session.UserId);

                var msg = new CEMsgAchievements
                {
                    PlayerAchievements = new HashSet<string>(playerAchievements),
                    AchievementPercentages = _cachedPercentages,
                };

                _netManager.ServerSendMessage(msg, session.Channel);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to send achievements to {session.Name}: {e}");
            }
        }
    }
}
