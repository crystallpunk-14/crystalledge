using Content.Server._CE.Procedural;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen.Components;
using Content.Shared._CE.WorldGen.Generators;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen.Generators;

/// <summary>
/// Renders one chunk's slice of a statically-stamped multi-chunk room.
/// All configuration comes from <see cref="CEWorldComponent.StaticRegions"/>, which is populated by the
/// <c>CEStampStructure</c> plan step. The generator itself carries no fields.
/// </summary>
public sealed partial class CEStaticRegionChunkGenerator : CEChunkGeneratorBase<CEStaticRegionChunkGenerator>
{
}

/// <summary>
/// Executes <see cref="CEStaticRegionChunkGenerator"/>. Looks up the chunk's region entry, snapshots the
/// room once in <see cref="BeginChunk"/>, then returns per-tile content from the correct slice.
/// </summary>
public sealed partial class CEStaticRegionChunkGeneratorSystem
    : CEChunkGeneratorSystem<CEStaticRegionChunkGenerator>
{
    [Dependency] private CEDungeonSystem _dungeon = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    private bool _hasRoom;
    private CERoomSnapshot? _snapshot;
    private CEStaticRegionEntry _entry;

    protected override void BeginChunk(CEStaticRegionChunkGenerator gen, CEChunkGenArgs args)
    {
        _hasRoom = false;
        _snapshot = null;

        var worldQuery = EntityQueryEnumerator<CEWorldComponent>();
        if (!worldQuery.MoveNext(out _, out var world))
        {
            Log.Error("CEStaticRegionChunkGenerator: no CEWorldComponent found.");
            return;
        }

        if (!world.StaticRegions.TryGetValue(args.ChunkCoord, out _entry))
        {
            Log.Error(
                $"CEStaticRegionChunkGenerator: no entry for chunk {args.ChunkCoord}. " +
                "Was CEStampStructure applied for this chunk?");
            return;
        }

        var room = _protoMan.Index(_entry.Room);
        _snapshot = _dungeon.ReadRoomRegion(room, _entry.RotationSteps);
        if (_snapshot == null)
        {
            Log.Error($"CEStaticRegionChunkGenerator: ReadRoomRegion returned null for '{_entry.Room}'.");
            return;
        }

        _hasRoom = true;
    }

    protected override void GenerateTile(CEStaticRegionChunkGenerator gen, in CETileGenContext ctx, ref CETileContent result)
    {
        if (!_hasRoom)
            return;

        var tileX = (ctx.ChunkCoord.X - _entry.At.X) * CEWorldComponent.ChunkSize + ctx.LocalTile.X;
        var tileY = (ctx.ChunkCoord.Y - _entry.At.Y) * CEWorldComponent.ChunkSize + ctx.LocalTile.Y;
        var level = (ctx.ChunkCoord.Z - _entry.At.Z) * CEWorldComponent.ChunkHeight + ctx.Level;
        result = _snapshot!.At(level, tileX, tileY);
    }
}
