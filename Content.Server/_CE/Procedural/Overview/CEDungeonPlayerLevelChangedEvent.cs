namespace Content.Server._CE.Procedural.Overview;

/// <summary>
/// Broadcast event raised on the server whenever a player tracked by
/// <see cref="Content.Shared._CE.Procedural.Components.CEDungeonPlayerComponent"/>
/// transitions between dungeon levels (or enters/leaves one).
/// Open dungeon-overview BUIs listen to this and re-push their state so player
/// counts and the "current level" highlight stay in sync.
/// </summary>
[ByRefEvent]
public readonly record struct CEDungeonPlayerLevelChangedEvent(
    EntityUid Player,
    string? FromLevelId,
    string? ToLevelId);
