using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Core.Components;

/// <summary>
/// When placed on a status effect entity, transforms it combined with another incoming status effect
/// into a new status effect, consuming both and applying a replacement with their combined stacks.
/// </summary>
[RegisterComponent]
public sealed partial class CEStatusEffectTransformComponent : Component
{
    /// <summary>
    /// Maps incoming status effect prototype ID → resulting combined status effect prototype ID.
    /// When a matching incoming status effect is applied to the same target, both this effect and the
    /// incoming effect are removed, and the resulting effect is applied with the combined stack count.
    /// The applier (source) of the incoming application is preserved on the new effect.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, EntProtoId> Transforms = new();
}
