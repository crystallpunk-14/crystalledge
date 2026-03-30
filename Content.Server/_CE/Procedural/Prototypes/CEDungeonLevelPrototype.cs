using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.PostProcess;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Server._CE.Procedural.Prototypes;

/// <summary>
/// Defines a single dungeon level: how it should be generated and any metadata.
/// Referenced by ID from dungeon zone or spawning systems.
/// </summary>
[Prototype("dungeonLevel")]
public sealed partial class CEDungeonLevelPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CEDungeonLevelPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <summary>
    /// The generator config that determines how this level's map is created.
    /// Uses polymorphic YAML deserialization (e.g. <c>!type:CEStaticMapConfig</c>).
    /// </summary>
    [DataField(required: true)]
    [AlwaysPushInheritance]
    public CEDungeonGeneratorConfig Config = default!;

    /// <summary>
    /// Optional human-readable name for this level, used for debugging / admin tools.
    /// </summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    /// Whether this level is stable (singleton — one instance per server, e.g. safe zones)
    /// or unstable (multiple instances allowed, cleaned up when empty).
    /// </summary>
    [DataField]
    public bool Stable;

    /// <summary>
    /// Maps exit slot names to the target dungeon level prototypes.
    /// After generation, exit entities with matching <c>ExitSlot</c> values get
    /// their <c>TargetLevel</c> assigned from this dictionary.
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<CEDungeonLevelPrototype>> Exits = new();

    [DataField]
    [AlwaysPushInheritance]
    public List<CEDungeonPostProcessLayer> PostProcess = new();
}
