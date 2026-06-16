using Content.Server._CE.WorldGen.Prototypes;
using Content.Shared._CE.Maths;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Components;

/// <summary>
/// Runtime state of one procedurally generated world. Added onto the z-network entity
/// (alongside <c>CEZLevelsNetworkComponent</c>) — never onto an individual map, since the
/// chunk map and modified-tile ledger are shared across every z-level of the world.
/// Server-only; reached from a player via that map's network uid.
/// </summary>
[RegisterComponent]
public sealed partial class CEWorldComponent : Component
{
    /// <summary>
    /// Recipe this world was generated from.
    /// </summary>
    [DataField]
    public ProtoId<CEWorldConfigPrototype> Config;

    /// <summary>
    /// Resolved world seed.
    /// </summary>
    [ViewVariables]
    public int Seed;

    /// <summary>
    /// Painted chunk map: (x, y, chunkZ) -> chunk type. Cells absent here stay void.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> ChunkMap = new();

    /// <summary>
    /// Chunks currently generated.
    /// </summary>
    [ViewVariables]
    public HashSet<Vector3i> LoadedChunks = new();

    /// <summary>
    /// Tiles edited by players that must survive unload/reload — keyed by chunk, then by
    /// depth offset (which z-level of the chunk), then the set of tile indices.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, Dictionary<int, HashSet<Vector2i>>> ModifiedTiles = new();

    /// <summary>
    /// Entities spawned per loaded chunk, for deterministic cleanup on unload.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, List<(int Level, EntityUid Ent, Vector2i Tile)>> LoadedEntities = new();

    /// <summary>
    /// Snapshot of tiles the generator placed per loaded chunk, for unload diffing.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, List<(int Level, Vector2i Tile, Tile Value)>> GeneratedTiles = new();
}
