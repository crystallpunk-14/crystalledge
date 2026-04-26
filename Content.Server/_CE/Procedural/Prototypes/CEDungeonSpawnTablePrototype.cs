using Content.Server._CE.Procedural.PostProcess;
using Content.Shared._CE.Procedural;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Prototypes;

/// <summary>
/// Defines a reusable spawn pool: entries, placement filters and flags — but NOT a budget.
/// Referenced from <see cref="CETableBudgetSpawnPostProcess"/> to decouple the
/// "what can spawn" definition from the per-level budget value.
/// </summary>
[Prototype("dungeonSpawnTable")]
public sealed partial class CEDungeonSpawnTablePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Weighted list of entries that can be spawned.
    /// </summary>
    [DataField(required: true)]
    public List<BudgetSpawnEntry> Entries = new();

    /// <summary>
    /// If set, only spawn on tiles whose prototype ID is in this list.
    /// </summary>
    [DataField]
    public List<ProtoId<ContentTileDefinition>>? TileWhitelist;

    /// <summary>
    /// If set, only spawn on tiles that have at least one anchored entity matching this whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? AnchoredWhitelist;

    /// <summary>
    /// If true, spawned GOAP mobs have their sleeping component removed so they are immediately active.
    /// </summary>
    [DataField]
    public bool WakeOnSpawn;

    /// <summary>
    /// Room types to exclude from spawning.
    /// </summary>
    [DataField]
    public List<CEProceduralRoomType> ExcludedRoomTypes = new();

    /// <summary>
    /// If true, only spawn on the main z-level. When false, spawns across all z-levels.
    /// </summary>
    [DataField]
    public bool MainZLevelOnly = true;
}
