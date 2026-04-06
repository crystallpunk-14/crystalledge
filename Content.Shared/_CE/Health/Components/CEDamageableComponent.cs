using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores accumulated damage per type for an entity.
/// Damage starts at 0 per type and increases when the entity is hurt.
/// <see cref="TotalDamage"/> is a cached sum of all per-type values, updated by the system.
/// Uses manual <see cref="ComponentGetState"/>/<see cref="ComponentHandleState"/>
/// (not AutoGenerateComponentState) so events can be raised cleanly.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CESharedDamageableSystem))]
public sealed partial class CEDamageableComponent : Component
{
    /// <summary>
    /// Accumulated damage broken down by type.
    /// Only types that have been applied appear as keys.
    /// </summary>
    [DataField, ViewVariables]
    public Dictionary<ProtoId<CEDamageTypePrototype>, int> Damage = new();

    /// <summary>
    /// Total damage across all types. Computed from <see cref="Damage"/>.
    /// </summary>
    [ViewVariables]
    public int TotalDamage
    {
        get
        {
            var total = 0;
            foreach (var v in Damage.Values)
            {
                total += v;
            }

            return total;
        }
    }
}

[Serializable, NetSerializable]
public sealed class CEDamageableComponentState : ComponentState
{
    public Dictionary<ProtoId<CEDamageTypePrototype>, int> Damage = new();
}
