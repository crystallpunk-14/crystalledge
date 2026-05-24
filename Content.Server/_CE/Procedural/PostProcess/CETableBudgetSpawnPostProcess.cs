using System.Threading.Tasks;
using Content.Server._CE.Procedural.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: resolves a <see cref="CEDungeonSpawnTablePrototype"/> by ID and runs a
/// budget spawn using the table's entries and filters. Decouples spawn-table definitions from
/// per-level budget values, allowing PvE and PvP levels to share a single table definition
/// while specifying different budgets.
/// </summary>
public sealed partial class CETableBudgetSpawnPostProcess : CEDungeonPostProcessLayer
{
    /// <summary>
    /// The spawn table prototype defining which entities can spawn and placement filters.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEDungeonSpawnTablePrototype> Table;

    /// <summary>
    /// Total budget available for this spawn pass.
    /// </summary>
    [DataField(required: true)]
    public int Budget;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, int mainZLevel, Func<ValueTask> suspend)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var table = protoMan.Index(Table);

        var layer = new CEBudgetSpawnPostProcess
        {
            Budget = Budget,
            Entries = table.Entries,
            TileWhitelist = table.TileWhitelist,
            AnchoredWhitelist = table.AnchoredWhitelist,
            MainZLevelOnly = table.MainZLevelOnly,
        };

        await layer.Execute(entMan, mapUid, mainZLevel, suspend);
    }
}
