using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Procedural.Admin;

[Serializable, NetSerializable]
public enum CEAdminDungeonOverviewUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CEAdminDungeonOverviewPlayerEntry
{
    public NetEntity Entity;
    public string CharacterName = string.Empty;
    public string AccountName = string.Empty;
}

[Serializable, NetSerializable]
public sealed class CEAdminDungeonOverviewInstanceEntry
{
    public NetEntity Anchor;
    public List<CEAdminDungeonOverviewPlayerEntry> Players = new();
}

[Serializable, NetSerializable]
public sealed class CEAdminDungeonOverviewLevelEntry
{
    public string Id = string.Empty;
    public string? NameLocId;
    public string? DescLocId;
    public Vector2i UIPosition;
    public SpriteSpecifier? Icon;
    public bool Stable;

    /// <summary>Target level IDs this level connects to (directed: this -> target).</summary>
    public List<string> Exits = new();

    public List<CEAdminDungeonOverviewInstanceEntry> Instances = new();
}

[Serializable, NetSerializable]
public sealed class CEAdminDungeonOverviewState : BoundUserInterfaceState
{
    public List<CEAdminDungeonOverviewLevelEntry> Levels = new();

    /// <summary>Prototype id of the level the admin who opened the UI is currently on.</summary>
    public string? CurrentLevelId;
}

[Serializable, NetSerializable]
public sealed class CEAdminDungeonOverviewTeleportMsg : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public CEAdminDungeonOverviewTeleportMsg(NetEntity target)
    {
        Target = target;
    }
}
