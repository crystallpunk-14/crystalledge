using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TempShield;

/// <summary>
/// Status effect component for temporary shields.
/// Each stack absorbs a configurable amount of damage from specific damage types.
/// Stacks passively decay over time via the status effect stack system.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CETempShieldStatusEffectComponent : Component
{
    /// <summary>
    /// How much damage each stack absorbs before being consumed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int AbsorbPerStack = 1;

    /// <summary>
    /// Which damage types this shield absorbs.
    /// If empty, absorbs all damage types.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<CEDamageTypePrototype>> AbsorbedTypes = new() { "Physical" };

    [DataField]
    public EntProtoId? BreakEffect;

    [DataField]
    public EntProtoId? TakeDamageEffect;
}
