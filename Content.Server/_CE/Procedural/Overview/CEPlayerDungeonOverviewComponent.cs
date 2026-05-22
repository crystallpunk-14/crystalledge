namespace Content.Server._CE.Procedural.Overview;

/// <summary>
/// Marker for entities (e.g. wayfinding stones) that expose the player-facing
/// dungeon overview UI on <see cref="Content.Shared._CE.Procedural.Overview.CEPlayerDungeonOverviewUiKey.Key"/>.
/// </summary>
[RegisterComponent]
public sealed partial class CEPlayerDungeonOverviewComponent : Component;
