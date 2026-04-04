using System.Threading.Tasks;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;

namespace Content.Server._CE.Procedural.PostProcess;

public sealed class CEDungeonPostProcessSystem : EntitySystem
{
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    internal async Task RunAll(
        List<CEDungeonPostProcessLayer> layers,
        EntityUid mapUid,
        int mainZLevel,
        Func<ValueTask> suspend)
    {
        foreach (var layer in layers)
        {
            await layer.Execute(EntityManager, mapUid, mainZLevel, suspend);
        }
    }

    /// <summary>
    /// Collects every map entity belonging to the z-network that contains
    /// <paramref name="mapUid"/>, or just <paramref name="mapUid"/> itself
    /// if no z-network exists.
    /// </summary>
    internal List<EntityUid> GetAllMaps(EntityUid mapUid)
    {
        var maps = new List<EntityUid>();

        if (_zLevels.TryGetZNetwork(mapUid, out var zNet))
        {
            foreach (var (_, zMapUid) in zNet.Value.Comp.ZLevels)
            {
                if (zMapUid != null)
                    maps.Add(zMapUid.Value);
            }
        }
        else
        {
            maps.Add(mapUid);
        }

        return maps;
    }

    /// <summary>
    /// Returns the map entity at the given z-level depth within the z-network
    /// that <paramref name="mapUid"/> belongs to.
    /// Falls back to <paramref name="mapUid"/> if no z-network exists or the depth is not found.
    /// </summary>
    internal EntityUid GetMapAtZLevel(EntityUid mapUid, int depth)
    {
        if (!_zLevels.TryGetZNetwork(mapUid, out var zNet))
            return mapUid;

        var zLevels = zNet.Value.Comp.ZLevels;
        return zLevels.TryGetValue(depth, out var target) && target != null
            ? target.Value
            : mapUid;
    }
}
