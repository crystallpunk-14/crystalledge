using Content.Shared._CE.Achievements;
using Robust.Shared.Network;

namespace Content.Client._CE.Achievements;

/// <summary>
/// Client-side system that receives and caches achievement data from the server.
/// </summary>
public sealed class CEAchievementsSystem : EntitySystem
{
    [Dependency] private readonly IClientNetManager _netManager = default!;

    /// <summary>
    /// Achievement prototype IDs that the current player has earned.
    /// </summary>
    public HashSet<string> PlayerAchievements { get; private set; } = new();

    /// <summary>
    /// Percentage of all players who have each achievement (0–100).
    /// </summary>
    public Dictionary<string, float> AchievementPercentages { get; private set; } = new();

    /// <summary>
    /// Whether data has been received from the server at least once.
    /// </summary>
    public bool DataLoaded { get; private set; }

    /// <summary>
    /// Fired when achievement data is received from the server.
    /// </summary>
    public event Action? AchievementsUpdated;

    public override void Initialize()
    {
        base.Initialize();

        _netManager.RegisterNetMessage<MsgCEAchievements>(OnAchievementsReceived);
    }

    private void OnAchievementsReceived(MsgCEAchievements msg)
    {
        PlayerAchievements = msg.PlayerAchievements;
        AchievementPercentages = msg.AchievementPercentages;
        DataLoaded = true;

        AchievementsUpdated?.Invoke();
    }
}
