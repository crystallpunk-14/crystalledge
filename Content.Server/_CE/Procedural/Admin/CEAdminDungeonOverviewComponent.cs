namespace Content.Server._CE.Procedural.Admin;

/// <summary>
/// Marker attached to entities that expose the admin dungeon overview BUI
/// (e.g. <c>AdminObserver</c>). Used to dispatch BUI messages to the system.
/// </summary>
[RegisterComponent]
public sealed partial class CEAdminDungeonOverviewComponent : Component;
