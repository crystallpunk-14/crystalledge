using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._CE.Procedural.Components;

/// <summary>
/// Marker component for entities that keep a dungeon instance alive.
/// Added to player species prototypes (e.g. <c>CEBaseSpeciesMob</c>).
/// Only living entities with this component prevent unstable instance cleanup.
/// Mobs and bosses should NOT have this component.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonPlayerComponent : Component
{
    /// <summary>
    /// Game time at which this player entity was initialized (round start offset).
    /// Set server-side on MapInit; used to compute how long a player took to reach each level.
    /// </summary>
    [DataField]
    public TimeSpan SessionStartedAt;
}
