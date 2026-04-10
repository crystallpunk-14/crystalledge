namespace Content.Server._CE.Procedural.Instance.Components;

/// <summary>
/// Marks an entity as an entry point inside a generated dungeon instance.
/// Incoming players materialize at the position of an active entry.
/// Once used (or after <see cref="DeactivateAt"/> expires), the entry becomes permanently inactive.
/// </summary>
[RegisterComponent]
public sealed partial class CEDungeonEntryPointComponent : Component
{
    /// <summary>
    /// Whether this entry point can still accept incoming players.
    /// Set to false after a group enters through it or after <see cref="DeactivateAt"/>.
    /// </summary>
    [DataField]
    public bool Active = true;

    /// <summary>
    /// If stable - never deactivates. Used for stable safezones.
    /// </summary>
    [DataField]
    public bool Stable = false;

    /// <summary>
    /// Game time after which this entry automatically deactivates.
    /// Prevents late joins into long-running dungeon runs.
    /// </summary>
    [DataField]
    public TimeSpan DeactivateAt = TimeSpan.MaxValue;

    /// <summary>
    /// How long after instance creation the entry stays active.
    /// Used by the instance system to compute <see cref="DeactivateAt"/>.
    /// </summary>
    [DataField]
    public TimeSpan ActiveDuration = TimeSpan.FromMinutes(2);
}
