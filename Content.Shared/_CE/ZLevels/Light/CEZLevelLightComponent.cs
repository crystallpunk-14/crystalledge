/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

namespace Content.Shared._CE.ZLevels.Light;

/// <summary>
/// Placed on an entity that has a PointLightComponent to propagate light
/// through transparent tiles up and down to adjacent Z-levels.
/// Proxy light entities are spawned on each traversed level with linearly attenuated energy.
/// </summary>
[RegisterComponent]
public sealed partial class CEZLevelLightComponent : Component
{
    /// <summary>
    /// Energy subtracted per Z-level of propagation (linear falloff).
    /// A proxy N levels away has energy = original_energy − N × EnergyStep.
    /// Propagation stops when that value falls below <see cref="MinEnergy"/>.
    /// </summary>
    [DataField]
    public float EnergyStep = 0.4f;

    /// <summary>
    /// Propagation stops when the computed proxy energy would fall below this threshold.
    /// </summary>
    [DataField]
    public float MinEnergy = 0.1f;

    /// <summary>
    /// How often the proxy set is fully rebuilt (tile transparency re-evaluated).
    /// Proxy positions are still updated immediately on every MoveEvent.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// When true, one extra "Lowest" proxy is spawned one level below the first
    /// solid floor tile reached during downward propagation.
    /// The Lowest proxy always has shadows disabled, regardless of the source setting.
    /// </summary>
    [DataField]
    public bool SpawnLowestProxy = true;

    // ── Runtime state (not serialised) ───────────────────────────────────

    /// <summary>When the next periodic full rebuild is due.</summary>
    [ViewVariables]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// UID of the "Lowest" proxy entity (spawned one level below the last solid floor hit).
    /// <see cref="EntityUid.Invalid"/> when absent.
    /// </summary>
    [ViewVariables]
    public EntityUid LowestProxy = EntityUid.Invalid;

    /// <summary>
    /// Runtime list of regular proxy light entity UIDs.
    /// Ordering: down-proxies (level−1, level−2, …) followed by up-proxies (level+1, …).
    /// Not serialised – rebuilt on ComponentStartup.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Proxies = new();
}
