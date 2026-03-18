using Content.Server._CE.Procedural.Generators;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Prototypes;

/// <summary>
/// Defines a single dungeon level: how it should be generated and any metadata.
/// Referenced by ID from dungeon zone or spawning systems.
/// </summary>
[Prototype("dungeonLevel")]
public sealed partial class CEDungeonLevelPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The generator config that determines how this level's map is created.
    /// Uses polymorphic YAML deserialization (e.g. <c>!type:CEStaticMapConfig</c>).
    /// </summary>
    [DataField(required: true)]
    public CEDungeonGeneratorConfig Config = default!;

    /// <summary>
    /// Optional human-readable name for this level, used for debugging / admin tools.
    /// </summary>
    [DataField]
    public string Name = string.Empty;
}
