using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Instance.Components;

/// <summary>
/// Attached to the z-network entity (or standalone map entity for non-z-network generators)
/// of a generated dungeon instance. Tracks instance metadata: prototype, stability,
/// player count, creation time, and the primary map used for player lookups.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonInstanceComponent : Component
{
    /// <summary>
    /// The dungeon level prototype this instance was generated from.
    /// </summary>
    [DataField]
    public ProtoId<CEDungeonLevelPrototype> PrototypeId;

    /// <summary>
    /// Whether this is a stable instance (singleton, always exists) or
    /// unstable (can have multiple instances, cleaned up when empty).
    /// </summary>
    [DataField]
    public bool Stable;

    /// <summary>
    /// When this instance was created (game time).
    /// </summary>
    [DataField]
    public TimeSpan CreatedAt;

    /// <summary>
    /// Game time when this instance first became empty (no players on any map).
    /// Null while players are present. Used by cleanup logic to determine when to delete.
    /// </summary>
    [DataField]
    public TimeSpan? EmptySince;
}
