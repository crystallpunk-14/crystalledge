using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Prototypes;
using Content.Server._CE.ZLevels.Core;
using Content.Server.Decals;
using Content.Shared._CE.Procedural;
using Content.Shared.Maps;
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
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly List<(Vector2i, Tile)> _tiles = new();

    public static readonly ProtoId<ContentTileDefinition> FallbackTileId = "CEStone";

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        InitializeRooms();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CEDungeonRoom3DPrototype>())
            InvalidateRoomPasswayCache();
    }

    /// <summary>
    /// Generates a dungeon level from the given prototype ID.
    /// Creates a new map and populates it according to the prototype's generator config.
    /// </summary>
    /// <returns>The generation result containing the created map info, or a failure result.</returns>
    public CEDungeonGenerateResult GenerateLevel(ProtoId<CEDungeonLevelPrototype> protoId)
    {
        if (!_proto.TryIndex(protoId, out var proto))
        {
            Log.Error($"CEDungeonSystem: unknown dungeon level prototype '{protoId}'.");
            return new CEDungeonGenerateResult(false);
        }

        return GenerateLevel(proto);
    }

    /// <summary>
    /// Generates a dungeon level from the given prototype.
    /// </summary>
    public CEDungeonGenerateResult GenerateLevel(CEDungeonLevelPrototype proto)
    {
        var result = proto.Config.Generate(EntityManager);

        if (!result.Success || result.MapUid is null)
        {
            Log.Error($"CEDungeonSystem: generation failed for dungeon level '{proto.ID}'.");
            return result;
        }

        _meta.SetEntityName(result.MapUid.Value, $"{proto.ID}");

        Log.Info($"CEDungeonSystem: generated dungeon level '{proto.ID}' on map {result.MapId}.");
        return result;
    }
}
