using Content.Shared._CE.Health;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.EntityEffect.Systems;

/// <summary>
/// Applies AoE effects from <see cref="CEEntityEffect"/> list when the entity is destroyed
/// via <see cref="CEDestructedEvent"/> (raised by <see cref="CEDestructibleSystem"/>).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDestructionEffectComponent : Component
{
    /// <summary>
    /// Effects applied to each entity in the area on destruction.
    /// </summary>
    [DataField(required: true)]
    public List<CEEntityEffect> Effects = new();

    /// <summary>
    /// Radius of the AoE effect around the destruction point.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    /// <summary>
    /// Maximum number of entities to affect. 0 = unlimited.
    /// </summary>
    [DataField]
    public int MaxTargets;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
