using Robust.Shared.Map;

namespace Content.Shared._CE.ElementInteraction;

/// <summary>
/// Raised as a broadcast event before fire stacks are applied to an entity.
/// Handlers can modify Stacks or set Cancelled to prevent ignition.
/// </summary>
[ByRefEvent]
public record struct CEIgniteEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before frost/cold stacks are applied to an entity.
/// Handlers can modify Stacks or set Cancelled to prevent freezing.
/// </summary>
[ByRefEvent]
public record struct CEFreezeEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before fire is placed on a tile.
/// Handlers can modify Stacks or set Cancelled to prevent ignition.
/// </summary>
[ByRefEvent]
public record struct CEIgniteTileAttemptEvent(MapCoordinates Coordinates, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a broadcast event before ice is placed on a tile.
/// Handlers can set Cancelled to prevent freezing.
/// </summary>
[ByRefEvent]
public record struct CEFreezeTileAttemptEvent(MapCoordinates Coordinates, bool Cancelled);
