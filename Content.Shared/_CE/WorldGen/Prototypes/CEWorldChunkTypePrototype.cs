using Content.Shared._CE.Music;
using Content.Shared._CE.WorldGen.Generators;
using Content.Shared._CE.WorldGen.PostProcess;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._CE.WorldGen.Prototypes;

/// <summary>
/// One paintable kind of chunk (forest, caves, solid wall, ...).
/// Holds a single polymorphic <see cref="CEChunkGenerator"/> that decides how the
/// chunk's tiles and entities are produced across all of its z-levels.
/// </summary>
[Prototype("worldChunkType")]
public sealed partial class CEWorldChunkTypePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <inheritdoc/>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CEWorldChunkTypePrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc/>
    [AbstractDataField, NeverPushInheritance]
    public bool Abstract { get; private set; }

    /// <summary>
    /// Color used by the debug chunk overlay to identify this chunk type visually.
    /// </summary>
    [DataField]
    public Color DebugColor = Color.Gray;

    /// <summary>
    /// Optional localization key for the title shown in the cinematic popup the first time a
    /// player enters a chunk of this type. Leave null to never show a popup.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Optional localization key for the description shown in the cinematic popup. Leave null for none.
    /// </summary>
    [DataField]
    public LocId? Desc;

    /// <summary>
    /// Optional sound played for a player the first time they enter a chunk of this type.
    /// Leave null for no sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? EntrySound;

    /// <summary>
    /// Optional ambient music theme that plays while a player stands in a chunk of this type. Overrides
    /// any map-level theme. Leave null to fall back to the map's <see cref="CEMapAmbientMusicThemeComponent"/>.
    /// </summary>
    [DataField]
    public ProtoId<CEAmbientMusicPrototype>? AmbientMusic;

    /// <summary>
    /// How this chunk's content is generated. Server-only: not deserialized on client.
    /// </summary>
    [DataField(serverOnly: true)]
    public CEChunkGenerator Generator = default!;

    /// <summary>
    /// Ordered per-tile post-process layers applied after generation. Server-only: not deserialized on client.
    /// </summary>
    [DataField(serverOnly: true)]
    [AlwaysPushInheritance]
    public List<CEWorldPostProcessLayer> PostProcess = new();
}