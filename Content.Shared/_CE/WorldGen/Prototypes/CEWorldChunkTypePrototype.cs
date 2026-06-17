using Content.Shared._CE.WorldGen.Generators;
using Content.Shared._CE.WorldGen.PostProcess;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.WorldGen.Prototypes;

/// <summary>
/// One paintable kind of chunk (forest, caves, solid wall, ...).
/// Holds a single polymorphic <see cref="CEChunkGenerator"/> that decides how the
/// chunk's tiles and entities are produced across all of its z-levels.
/// </summary>
[Prototype("worldChunkType")]
public sealed partial class CEWorldChunkTypePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Color used by the debug chunk overlay to identify this chunk type visually.
    /// </summary>
    [DataField]
    public Color DebugColor = Color.Gray;

    /// <summary>
    /// How this chunk's content is generated. Server-only: not deserialized on client.
    /// </summary>
    [DataField(serverOnly: true)]
    public CEChunkGenerator Generator = default!;

    /// <summary>
    /// Ordered per-tile post-process layers applied after generation. Server-only: not deserialized on client.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CEWorldPostProcessLayer> PostProcess = new();
}
