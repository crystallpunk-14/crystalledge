using System.Threading;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Generators.StaticZNetwork;

/// <summary>
/// Generator config that creates a z-level network from a <see cref="CEZLevelMapPrototype"/>.
/// Loads all maps defined in the prototype and links them into a z-network.
/// </summary>
public sealed partial class CEStaticZNetworkConfig : CEDungeonGeneratorConfigBase<CEStaticZNetworkConfig>
{
    /// <summary>
    /// The ID of the <see cref="CEZLevelMapPrototype"/> to use.
    /// Defines the list of map files and shared components for all z-levels.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CEZLevelMapPrototype> ZMapProto;
}

/// <summary>
/// Handles <see cref="CEStaticZNetworkConfig"/> by loading all maps from a
/// <see cref="CEZLevelMapPrototype"/> and linking them into a z-level network.
/// The primary map (depth 0) is reported back via the event.
/// </summary>
public sealed partial class CEStaticZNetworkGeneratorSystem : CEDungeonGeneratorSystem<CEStaticZNetworkConfig>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;

    protected override Job<CEDungeonGenerateResult> CreateJob(
        CEStaticZNetworkConfig config,
        double maxTime,
        CancellationToken cancellation)
    {
        return new CEDelegateDungeonJob(maxTime, () => GenerateZNetwork(config), cancellation);
    }

    private CEDungeonGenerateResult GenerateZNetwork(CEStaticZNetworkConfig config)
    {
        if (!_proto.TryIndex(config.ZMapProto, out var zMapProto))
        {
            Log.Error($"CEStaticZNetworkGeneratorSystem: unknown zMap prototype '{config.ZMapProto}'.");
            return new CEDungeonGenerateResult(false);
        }

        if (zMapProto.Maps.Count == 0)
        {
            Log.Error($"CEStaticZNetworkGeneratorSystem: zMap prototype '{config.ZMapProto}' has no maps.");
            return new CEDungeonGenerateResult(false);
        }

        // Create the z-network entity with shared components from the prototype.
        var network = _zLevels.CreateZNetwork(zMapProto.Components);

        var mapsByDepth = new Dictionary<EntityUid, int>();
        EntityUid? primaryMapUid = null;

        // Load each map file at sequential depths.
        var depth = 0;
        foreach (var path in zMapProto.Maps)
        {
            if (!_loader.TryLoadMap(path, out var mapEnt, out _))
            {
                Log.Error($"CEStaticZNetworkGeneratorSystem: failed to load map at depth {depth} from '{path}'.");
                return new CEDungeonGenerateResult(false);
            }

            mapsByDepth.Add(mapEnt.Value, depth);

            // Depth 0 is the primary map.
            if (depth == 0)
                primaryMapUid = mapEnt.Value.Owner;

            depth++;
        }

        // Link all maps into the z-network.
        if (!_zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth))
        {
            Log.Error($"CEStaticZNetworkGeneratorSystem: failed to link maps into z-network for '{config.ZMapProto}'.");
            return new CEDungeonGenerateResult(false);
        }

        // Initialize all maps in the network.
        _zLevels.InitializeZNetwork(network);

        // Report the primary map back.
        MapId? mapId = null;
        if (primaryMapUid != null && TryComp<MapComponent>(primaryMapUid.Value, out var mapComp))
            mapId = mapComp.MapId;

        return new CEDungeonGenerateResult(true, primaryMapUid, mapId);
    }
}
