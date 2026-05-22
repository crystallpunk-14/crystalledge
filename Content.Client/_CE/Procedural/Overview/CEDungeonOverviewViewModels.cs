using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Client._CE.Procedural.Overview;

/// <summary>
/// Per-level data passed to <see cref="CEDungeonOverviewControl"/>.
/// Both admin and player overview windows convert their server state into this shape.
/// </summary>
public sealed class CEDungeonOverviewLevelView
{
    public string Id = string.Empty;
    public string? NameLocId;
    public string? DescLocId;
    public Vector2i UIPosition;
    public SpriteSpecifier? Icon;
    public bool Stable;
    public List<string> Exits = new();

    /// <summary>Total players on this level across all instances.</summary>
    public int PlayerCount;

    /// <summary>
    /// Optional per-instance breakdown. <c>null</c> hides the instance breakdown
    /// section entirely (used for the player-facing UI).
    /// </summary>
    public List<CEDungeonOverviewInstanceView>? Instances;
}

public sealed class CEDungeonOverviewInstanceView
{
    public int PlayerCount;

    /// <summary>
    /// Optional list of player entries shown under the instance.
    /// <c>null</c> hides the player list (only the count is shown).
    /// </summary>
    public List<CEDungeonOverviewPlayerView>? Players;
}

public sealed class CEDungeonOverviewPlayerView
{
    public NetEntity Entity;
    public string CharacterName = string.Empty;
    public string AccountName = string.Empty;
}
