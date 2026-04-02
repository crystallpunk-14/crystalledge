using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Frost;

/// <summary>
/// When present on an entity, frost effects (FreezeTile) will replace this entity
/// with <see cref="FreezesInto"/>, preserving rotation. Used on water tiles.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEFreezeTransformComponent : Component
{
    /// <summary>
    /// Entity prototype spawned when this entity is frozen.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId FreezesInto;
}
