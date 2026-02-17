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

        _netManager.RegisterNetMessage<MsgCEAchievements>();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        RefreshCachedPercentages();
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

            var msg = new MsgCEAchievements
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
}
