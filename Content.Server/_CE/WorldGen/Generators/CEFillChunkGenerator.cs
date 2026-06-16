using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Fills every tile of the chunk — across all of its z-levels — with a single tile type,
/// and optionally spawns and anchors one entity on every tile (e.g. a solid stone-wall chunk).
/// </summary>
public sealed partial class CEFillChunkGenerator : CEChunkGeneratorBase<CEFillChunkGenerator>
{
    /// <summary>
    /// Tile placed on every cell of every z-level of the chunk.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> FillTile;

    /// <summary>
    /// Entity spawned and anchored on every cell of every z-level (null == bare floor).
    /// </summary>
    [DataField]
    public EntProtoId? FillEntity;
}

/// <summary>
/// Executes <see cref="CEFillChunkGenerator"/>: returns the fill tile (and optionally the fill
/// entity) for every cell. The base system batches writes, anchors entities and records bookkeeping.
/// </summary>
public sealed partial class CEFillChunkGeneratorSystem : CEChunkGeneratorSystem<CEFillChunkGenerator>
{
    protected override void GenerateTile(CEFillChunkGenerator gen, in CETileGenContext ctx, ref CETileContent result)
    {
        result.Tile = gen.FillTile;

        if (gen.FillEntity is { } proto)
            result.Entity = new CEEntitySpec(proto, Angle.Zero);
    }
}
