using System.Threading.Tasks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Post-process layer: replaces all entities of a specific prototype with another prototype,
/// rolled independently per entity with a configurable chance.
/// Lighter weight than <see cref="CEEntityReplacePostProcess"/> — matches by prototype ID
/// instead of a full <c>EntityWhitelist</c>.
/// </summary>
public sealed partial class CEEntitySwapPostProcess : CEDungeonPostProcessLayer
{
    /// <summary>
    /// Prototype ID of entities to replace.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Source = default!;

    /// <summary>
    /// Prototype to spawn in place of each matched entity.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Target = default!;

    /// <summary>
    /// Probability (0–1) that each matching entity is replaced.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;

    public override async Task Execute(IEntityManager entMan, EntityUid mapUid, Func<ValueTask> suspend)
    {
        var postProcess = entMan.System<CEDungeonPostProcessSystem>();
        var map = entMan.System<SharedMapSystem>();
        var metaQuery = entMan.GetEntityQuery<MetaDataComponent>();

        var random = new Random();
        var maps = postProcess.GetAllMaps(mapUid);
        var counter = 0;

        foreach (var uid in maps)
        {
            if (!entMan.TryGetComponent<MapGridComponent>(uid, out var grid))
                continue;

            var toReplace = new List<(EntityUid Ent, EntityCoordinates Coords)>();

            foreach (var tileRef in map.GetAllTiles(uid, grid))
            {
                if (++counter % 500 == 0)
                    await suspend();

                var anchored = map.GetAnchoredEntitiesEnumerator(uid, grid, tileRef.GridIndices);
                while (anchored.MoveNext(out var entUid))
                {
                    if (!metaQuery.TryGetComponent(entUid.Value, out var meta))
                        continue;

                    if (meta.EntityPrototype?.ID != Source.Id)
                        continue;

                    if (random.NextSingle() > Chance)
                        continue;

                    var coords = entMan.GetComponent<TransformComponent>(entUid.Value).Coordinates;
                    toReplace.Add((entUid.Value, coords));
                }
            }

            foreach (var (ent, coords) in toReplace)
            {
                entMan.DeleteEntity(ent);
                entMan.SpawnEntity(Target, coords);
            }
        }
    }
}
