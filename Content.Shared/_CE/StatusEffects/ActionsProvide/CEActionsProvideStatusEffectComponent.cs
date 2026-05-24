using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.ActionsProvide;

/// <summary>
/// When placed on a status effect entity, grants a list of actions to the entity the effect is applied to.
/// Actions are removed when the status effect ends.
/// </summary>
[RegisterComponent]
public sealed partial class CEActionsProvideStatusEffectComponent : Component
{
    /// <summary>
    /// The action prototypes to grant to the affected entity when the effect is applied.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Actions = new();

    /// <summary>
    /// Runtime-tracked action entity UIDs, used to remove actions when the status effect ends.
    /// Not serialized — mirrors the pattern used by GhostComponent.
    /// </summary>
    public List<EntityUid> ActionEntities = new();
}
