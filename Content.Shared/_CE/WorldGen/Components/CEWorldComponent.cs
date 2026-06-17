using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.WorldGen.Components;

/// <summary>
/// Runtime state of one procedurally generated world. Added onto the z-network entity
/// (alongside <c>CEZLevelsNetworkComponent</c>) — never onto an individual map, since the
/// chunk map and modified-tile ledger are shared across every z-level of the world.
/// Reached from a player via that map's network uid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEWorldComponent : Component
{
    /// <summary>Side length of one chunk in tiles.</summary>
    public const int ChunkSize = 8;

    /// <summary>How many z-levels a single chunk spans.</summary>
    public const int ChunkHeight = 1;

    /// <summary>Chunks to keep loaded outward from each player.</summary>
    public const float LoadRadiusChunks = 2f;


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
    /// Networked so the client overlay can visualize chunk types.
    /// </summary>
    [DataField, AutoNetworkedField]
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

    /// <summary>
    /// Per-chunk data for statically-stamped structures. Written by <c>CEStampStructure</c> plan step,
    /// read by <c>CEStaticRegionChunkGenerator</c> at chunk load time.
    /// </summary>
    [ViewVariables]
    public Dictionary<Vector3i, CEStaticRegionEntry> StaticRegions = [];
}

/// <summary>
/// Describes which room to render for one chunk that belongs to a stamped static structure.
/// </summary>
public record struct CEStaticRegionEntry(
    ProtoId<CEDungeonRoom3DPrototype> Room,
    Vector3i At,
    int RotationSteps);
