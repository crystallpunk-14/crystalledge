using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Immunity;

/// <summary>
/// When present on a status effect entity, blocks application of the listed status effects
/// to the entity that owns this status effect.
///
/// Intercepts <see cref="Content.Shared._CE.EntityEffect.Effects.CEAttemptApplyStatusEffectEvent"/>
/// and <see cref="Content.Shared._CE.EntityEffect.Effects.CEAttemptApplyStatusEffectStackEvent"/>
/// via the StatusEffectRelayedEvent relay mechanism.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEStatusEffectImmunityComponent : Component
{
    /// <summary>
    /// The list of status effect prototype IDs that are blocked.
    /// If empty, no effects are blocked.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> BlockedEffects = new();
}
