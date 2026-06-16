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
    /// <summary>Recipe this world was generated from.</summary>
    [DataField]
    public ProtoId<CEWorldConfigPrototype> Config;

    /// <summary>Resolved world seed.</summary>
    [ViewVariables]
    public int Seed;

    /// <summary>Side length of a chunk in tiles (copied from the config).</summary>
    [ViewVariables]
    public int ChunkSize = 8;

    /// <summary>How many chunks out from each player to keep loaded (copied from the config).</summary>
    [ViewVariables]
    public float LoadRadiusChunks = 2f;

    /// <summary>Painted chunk map: (x, y, chunkZ) -> chunk type. Cells absent here stay void.</summary>
    [ViewVariables]
    public Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> ChunkMap = new();

    /// <summary>z-level depth -> the grid map entity for that level.</summary>
    [ViewVariables]
    public Dictionary<int, EntityUid> ZLevelMaps = new();

    /// <summary>Lowest / highest painted chunk layer (derived from <see cref="ChunkMap"/>).</summary>
    [ViewVariables]
    public int MinChunkZ;

    [ViewVariables]
    public int MaxChunkZ;

    /// <summary>Chunks currently generated.</summary>
    [ViewVariables]
    public HashSet<Vector3i> LoadedChunks = new();

    /// <summary>
    /// Tiles edited by players that must survive unload/reload — keyed by chunk, then by
    /// depth offset (which z-level of the chunk), then the set of tile indices.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, Dictionary<int, HashSet<Vector2i>>> ModifiedTiles = new();

    /// <summary>Entities spawned per loaded chunk, for deterministic cleanup on unload.</summary>
    [ViewVariables]
    public Dictionary<Vector3i, List<(int Level, EntityUid Ent, Vector2i Tile)>> LoadedEntities = new();

    /// <summary>Snapshot of tiles the generator placed per loaded chunk, for unload diffing.</summary>
    [ViewVariables]
    public Dictionary<Vector3i, List<(int Level, Vector2i Tile, Tile Value)>> GeneratedTiles = new();
}
