using Robust.Shared.GameObjects;

namespace Content.Shared._CE.Procedural.Components;

/// <summary>
/// Marker component for entities that keep a dungeon instance alive.
/// Added to player species prototypes (e.g. <c>CEBaseSpeciesMob</c>).
/// Only living entities with this component prevent unstable instance cleanup.
/// Mobs and bosses should NOT have this component.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonPlayerComponent : Component;
