using System.Threading;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural.Generators.RandomZNetwork;

/// <summary>
/// Generator config that picks a <see cref="CEZLevelMapPrototype"/> at random using
/// weighted selection and then creates a z-level network from it.
/// </summary>
public sealed partial class CERandomZNetworkConfig : CEDungeonGeneratorConfigBase<CERandomZNetworkConfig>
{
    /// <summary>
    /// Weighted map variants. Each key is a <see cref="CEZLevelMapPrototype"/> ID,
    /// and the value is its relative weight for random selection.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<CEZLevelMapPrototype>, float> Variants = new();
}

public sealed partial class CERandomZNetworkGeneratorSystem : CEDungeonGeneratorSystem<CERandomZNetworkConfig>
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;

    protected override Job<CEDungeonGenerateResult> CreateJob(
        CERandomZNetworkConfig config,
        double maxTime,
        CancellationToken cancellation)
    {
        return new CEDelegateDungeonJob(maxTime, () => GenerateZNetwork(config), cancellation);
    }

    private CEDungeonGenerateResult GenerateZNetwork(CERandomZNetworkConfig config)
    {
        if (config.Variants.Count == 0)
        {
            Log.Error("CERandomZNetworkGeneratorSystem: Variants dictionary is empty.");
            return new CEDungeonGenerateResult(false);
        }

        // Validate and sum weights.
        var totalWeight = 0f;
        foreach (var (protoId, weight) in config.Variants)
        {
            if (!float.IsFinite(weight) || weight <= 0f)
            {
                Log.Error($"CERandomZNetworkGeneratorSystem: variant '{protoId}' has invalid weight {weight}. All weights must be finite and strictly positive.");
                return new CEDungeonGenerateResult(false);
            }

            totalWeight += weight;
        }

        var roll = _random.NextFloat() * totalWeight;
        ProtoId<CEZLevelMapPrototype>? chosen = null;
        var accumulated = 0f;
        foreach (var (protoId, weight) in config.Variants)
        {
            accumulated += weight;
            if (roll <= accumulated)
            {
                chosen = protoId;
                break;
            }
        }

        // Fallback: last entry (handles floating-point edge cases).
        if (chosen == null)
        {
            foreach (var protoId in config.Variants.Keys)
            {
                chosen = protoId;
            }
        }

        if (!_proto.TryIndex(chosen!.Value, out var zMapProto))
        {
            Log.Error($"CERandomZNetworkGeneratorSystem: unknown zMap prototype '{chosen}'.");
            return new CEDungeonGenerateResult(false);
        }

        if (zMapProto.Maps.Count == 0)
        {
            Log.Error($"CERandomZNetworkGeneratorSystem: zMap prototype '{chosen}' has no maps.");
            return new CEDungeonGenerateResult(false);
        }

        // Create the z-network with shared components from the prototype.
        var network = _zLevels.CreateZNetwork(zMapProto.Components);

        var mapsByDepth = new Dictionary<EntityUid, int>();
        EntityUid? primaryMapUid = null;

        var depth = 0;
        foreach (var path in zMapProto.Maps)
        {
            if (!_loader.TryLoadMap(path, out var mapEnt, out _))
            {
                Log.Error($"CERandomZNetworkGeneratorSystem: failed to load map at depth {depth} from '{path}'.");
                return new CEDungeonGenerateResult(false);
            }

            mapsByDepth.Add(mapEnt.Value, depth);

            if (depth == 0)
                primaryMapUid = mapEnt.Value.Owner;

            depth++;
        }

        if (!_zLevels.TryAddMapsIntoZNetwork(network, mapsByDepth))
        {
            Log.Error($"CERandomZNetworkGeneratorSystem: failed to link maps into z-network for '{chosen}'.");
            return new CEDungeonGenerateResult(false);
        }

        MapId? mapId = null;
        if (primaryMapUid != null && TryComp<MapComponent>(primaryMapUid.Value, out var mapComp))
            mapId = mapComp.MapId;

        return new CEDungeonGenerateResult(true, primaryMapUid, mapId, network.Owner);
    }
}
