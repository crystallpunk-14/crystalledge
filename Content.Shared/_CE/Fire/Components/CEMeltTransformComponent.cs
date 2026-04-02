using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Fire;

/// <summary>
/// When present on an entity, fire effects (melting) will replace this entity
/// with <see cref="MeltsInto"/>, preserving rotation. Used on ice tiles.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEMeltTransformComponent : Component
{
    /// <summary>
    /// Entity prototype spawned when this entity is melted by fire.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId MeltsInto;
}
