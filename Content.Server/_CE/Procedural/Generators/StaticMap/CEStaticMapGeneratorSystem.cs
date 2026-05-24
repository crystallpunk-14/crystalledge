using System.Threading;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._CE.Procedural.Generators.StaticMap;

/// <summary>
/// Generator config that loads a pre-made static map from a resource file.
/// The map is loaded as-is onto the dungeon level's map.
/// </summary>
public sealed partial class CEStaticMapConfig : CEDungeonGeneratorConfigBase<CEStaticMapConfig>
{
    /// <summary>
    /// Path to the map file to load.
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath;
}

/// <summary>
/// Handles <see cref="CEStaticMapConfig"/> by loading a pre-made map file from resources.
/// Creates a new map from the specified file path.
/// </summary>
public sealed partial class CEStaticMapGeneratorSystem : CEDungeonGeneratorSystem<CEStaticMapConfig>
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;

    protected override Job<CEDungeonGenerateResult> CreateJob(
        CEStaticMapConfig config,
        double maxTime,
        CancellationToken cancellation)
    {
        return new CEDelegateDungeonJob(maxTime,
            () =>
            {
                if (!_loader.TryLoadMap(config.MapPath, out var map, out _))
                {
                    Log.Error($"CEStaticMapGeneratorSystem: failed to load map from path '{config.MapPath}'.");
                    return new CEDungeonGenerateResult(false);
                }

                if (!TryComp<MapComponent>(map.Value, out var mapComp))
                {
                    Log.Error($"CEStaticMapGeneratorSystem: loaded entity {map.Value} has no MapComponent.");
                    return new CEDungeonGenerateResult(false);
                }

                return new CEDungeonGenerateResult(true, map.Value.Owner, mapComp.MapId);
            },
            cancellation);
    }
}
