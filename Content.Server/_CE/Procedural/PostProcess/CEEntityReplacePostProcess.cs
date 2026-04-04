using System.Threading.Tasks;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: replaces entities matching a whitelist with a target prototype,
/// rolled independently per entity with a configurable chance.
/// </summary>
public sealed partial class CEEntityReplacePostProcess : CEDungeonPostProcessLayer
{
    /// <summary>
    /// Whitelist filter for source entities. Only matching entities are eligible for replacement.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist Source = default!;

    /// <summary>
    /// Prototype to spawn in place of each replaced entity.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Target = default!;

    /// <summary>
    /// Probability (0–1) that each matching entity is replaced.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, int mainZLevel, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var whitelist = entMan.System<EntityWhitelistSystem>();

        var random = new Random();
        var maps = postProcess.GetAllMaps(mapUid);
        var counter = 0;

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            // Collect replacements first to avoid modifying the grid during enumeration.
            var toReplace = new List<(EntityUid Ent, EntityCoordinates Coords)>();

            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 500 == 0)
                    await suspend();

                var anchored = map.GetAnchoredEntitiesEnumerator(uid, grid, tileRef.GridIndices);
                while (anchored.MoveNext(out var entUid))
                {
                    if (!whitelist.IsValid(Source, entUid.Value))
                        continue;

                    if (random.NextSingle() > Chance)
                        continue;

                    var coords = entMan.GetComponent<TransformComponent>(entUid.Value).Coordinates;
                    toReplace.Add((entUid.Value, coords));
                }
            }

            // Apply replacements after iteration.
            foreach (var (ent, coords) in toReplace)
            {
                entMan.DeleteEntity(ent);
                entMan.SpawnEntity(Target, coords);
            }
        }
    }
}
