using Content.Server._CE.WorldGen.Generators;
using Content.Server._CE.WorldGen.PostProcess;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Prototypes;

/// <summary>
/// One paintable kind of chunk (forest, caves, solid wall, ...).
/// Holds a single polymorphic <see cref="CEChunkGenerator"/> that decides how the
/// chunk's tiles and entities are produced across all of its z-levels. Today only
/// fill generation exists; noise/layer generators can be added without touching the planner.
/// </summary>
[Prototype("worldChunkType")]
public sealed partial class CEWorldChunkTypePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// How this chunk's content is generated.
    /// </summary>
    [DataField(required: true)]
    public CEChunkGenerator Generator = default!;

    /// <summary>
    /// Ordered per-tile post-process layers applied after generation (empty == no post-processing).
    /// </summary>
    [DataField]
    public List<CEWorldPostProcessLayer> PostProcess = new();
}
