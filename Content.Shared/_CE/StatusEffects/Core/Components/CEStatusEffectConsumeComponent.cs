using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Core.Components;

/// <summary>
/// When placed on a status effect entity, absorbs incoming stacks of specified effects instead of
/// letting them apply separately. The incoming stacks are added to this effect, and the applier
/// (source) is updated to the incoming applier.
/// Example: cursed fire consumes regular fire — applying fire to an entity already cursed with
/// cursed fire just makes the cursed fire stronger rather than adding a separate fire stack.
/// </summary>
[RegisterComponent]
public sealed partial class CEStatusEffectConsumeComponent : Component
{
    /// <summary>
    /// Status effect prototype IDs that this effect will absorb.
    /// When one of these is applied to the same target, its stacks are redirected into this effect.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Consumes = new();
}
