using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.Core;

/// <summary>
/// When added to an entity, spawns a tile effect at that entity's location on <see cref="MapInitEvent"/>.
/// Useful for placing persistent tile effects via YAML map or entity prototypes.
/// </summary>
[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class CETileEffectSpawnerComponent : Component
{
    /// <summary>
    /// The tile effect entity prototype to spawn. Must have <see cref="CETileEffectComponent"/>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId TileEffect;

    /// <summary>
    /// Number of stacks to apply.
    /// </summary>
    [DataField]
    public int Amount = 1;

    /// <summary>
    /// Per-call stack cap. 0 means use the component's MaxStacks.
    /// </summary>
    [DataField]
    public int Max;
}
