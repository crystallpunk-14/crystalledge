using System.Numerics;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.PostProcess;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

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
    /// Localization key for the display name shown in the entry popup.
    /// When set, the client resolves this key in its own locale.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Localization key for the description shown in the entry popup.
    /// When set, the client resolves this key in its own locale.
    /// </summary>
    [DataField]
    public LocId? Desc;

    /// <summary>
    /// Optional sound played for a player when they see the entry popup for the first time.
    /// Leave null for no sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? EntrySound;

    /// <summary>
    /// Whether this level is stable (singleton — one instance per server, e.g. safe zones)
    /// or unstable (multiple instances allowed, cleaned up when empty).
    /// </summary>
    [DataField]
    public bool Stable;

    /// <summary>
    /// How long after this level instance is created its entry points remain eligible for selection.
    /// Once this duration has elapsed, new entrants can no longer be routed to this level's entry points.
    /// Leave null for no time limit.
    /// In YAML, bare numeric values are interpreted as seconds; time span strings such as <c>hh:mm:ss</c>
    /// may also be used where supported by the serializer.
    /// </summary>
    [DataField]
    public TimeSpan? MaxEntryTime;

    /// <summary>
    /// When true, the level is auto-generated at round start and registered as an instance.
    /// Used for entry levels: the default game map can be a placeholder (e.g. <c>Empty</c>),
    /// and players spawn into the round-start dungeon level via its embedded spawn points.
    /// Implies stability — round-start levels persist for the entire round.
    /// </summary>
    [DataField]
    public bool Roundstart;

    /// <summary>
    /// Maps exit slot numbers to the target dungeon level prototypes.
    /// After generation, exit entities with matching <c>TargetLevel</c> values get
    /// their resolved target assigned from this dictionary.
    /// </summary>
    [DataField]
    public Dictionary<int, ProtoId<CEDungeonLevelPrototype>> Exits = new();

    [DataField]
    [AlwaysPushInheritance]
    public List<CEDungeonPostProcessLayer> PostProcess = new();

    /// <summary>
    /// The z-level depth that post-processing layers should treat as the "main" level.
    /// Layers like budget spawn use this to restrict entity placement to a single z-level.
    /// </summary>
    [DataField]
    public int MainZLevel = 1;

    /// <summary>
    /// Position of this level's node on the admin dungeon overview graph (virtual pixels).
    /// </summary>
    [DataField("uiPosition")]
    public Vector2i UIPosition;

    /// <summary>
    /// Icon used for this level on the admin dungeon overview graph.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;
}
