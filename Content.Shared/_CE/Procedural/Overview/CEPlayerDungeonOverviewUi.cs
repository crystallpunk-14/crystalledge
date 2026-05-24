using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Procedural.Overview;

[Serializable, NetSerializable]
public enum CEPlayerDungeonOverviewUiKey : byte
{
    Key,
}

/// <summary>
/// Player-facing per-level entry. Carries no information about specific players —
/// only their counts on each level — to avoid leaking identities.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEPlayerDungeonOverviewLevelEntry
{
    public string Id = string.Empty;
    public string? NameLocId;
    public string? DescLocId;
    public Vector2i UIPosition;
    public SpriteSpecifier? Icon;
    public bool Stable;

    /// <summary>Target level IDs this level connects to (directed: this -&gt; target).</summary>
    public List<string> Exits = new();

    /// <summary>Total count of players currently on this level (across all instances).</summary>
    public int PlayerCount;
}

[Serializable, NetSerializable]
public sealed class CEPlayerDungeonOverviewState : BoundUserInterfaceState
{
    public List<CEPlayerDungeonOverviewLevelEntry> Levels = new();

    /// <summary>Prototype id of the level the BUI host (e.g. wayfinding stone) sits on.</summary>
    public string? CurrentLevelId;
}
