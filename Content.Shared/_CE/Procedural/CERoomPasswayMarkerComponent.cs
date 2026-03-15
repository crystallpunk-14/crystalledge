namespace Content.Shared._CE.Procedural;

/// <summary>
/// Marker component placed on room-border entities that indicate
/// where a passway (exit/entrance) exists in a dungeon room template.
/// The entity's <see cref="TransformComponent.LocalRotation"/> points
/// outward from the room — i.e. the direction the passage leads.
/// </summary>
[RegisterComponent]
public sealed partial class CERoomPasswayMarkerComponent : Component
{
}
