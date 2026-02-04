using Content.Server._CE.ZLevels.Core;
using Content.Server.Decals;
using Content.Server.GameTicking.Events;
using Content.Shared._CE.Procedural;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Procedural;

public sealed partial class CEDungeonSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    private readonly List<(Vector2i, Tile)> _tiles = new();

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public static readonly ProtoId<ContentTileDefinition> FallbackTileId = "CEStone";

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        InitializeRooms();
        InitializeCache();
    }
}
