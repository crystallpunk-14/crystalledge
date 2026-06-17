using Content.Server._CE.WorldGen.Generators;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.PostProcess;

/// <summary>
/// Per-tile worldgen post-process: replaces a specific entity prototype with another, rolled per-tile
/// with a configurable probability. Useful for biome variation (e.g. swap brick walls for stone/dirt
/// at low chance to break up uniformity).
/// </summary>
public sealed partial class CEEntitySwapWorldPostProcess : CEWorldPostProcessLayerBase<CEEntitySwapWorldPostProcess>
{
    /// <summary>
    /// Entity prototype to replace.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Source = default!;

    /// <summary>
    /// Replacement prototype.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Target = default!;

    /// <summary>
    /// Probability (0–1) that a matching entity is replaced.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;
}

/// <summary>
/// Executes <see cref="CEEntitySwapWorldPostProcess"/>.
/// </summary>
public sealed partial class CEEntitySwapWorldPostProcessSystem
    : CEWorldPostProcessSystem<CEEntitySwapWorldPostProcess>
{
    protected override void Process(ref CEWorldPostProcessEvent<CEEntitySwapWorldPostProcess> ev)
    {
        var layer = ev.Layer;

        if (ev.Content.Entity is not { } ent || ent.Proto != layer.Source)
            return;

        // Allocation-free deterministic float in [0,1) from the tile seed.
        var roll = (uint) HashCode.Combine(ev.LayerSeed, 0x9E3779B9) / (float) uint.MaxValue;
        if (roll > layer.Chance)
            return;

        ev.Content.Entity = new CEEntitySpec(layer.Target, ent.Rotation, ent.Anchored);
    }
}
