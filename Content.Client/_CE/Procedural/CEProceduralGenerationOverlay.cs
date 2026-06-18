using System.Numerics;
using Content.Shared._CE.Maths;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.WorldGen.Components;
using Content.Shared._CE.WorldGen.Prototypes;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Procedural;

/// <summary>
/// Debug overlay that visualizes:
/// <list type="bullet">
///   <item><see cref="CEGeneratingProceduralDungeonComponent"/> — abstract room graph (rooms + connections).</item>
///   <item><see cref="CEWorldComponent"/> — chunk grid in the loading radius around the player (colored by type).</item>
/// </list>
/// Toggled by <c>dungen_generation_visualize</c>.
/// </summary>
public sealed partial class CEProceduralGenerationOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    private readonly Font _font;

    private static readonly Color DefaultFillColor = Color.Gray.WithAlpha(0.3f);
    private static readonly Color DefaultBorderColor = Color.Gray;
    private static readonly Color ConnectionColor = Color.White.WithAlpha(0.6f);

    private const int BaseFontSize = 12;

    private readonly FontResource _fontResource;

    public CEProceduralGenerationOverlay()
    {
        IoCManager.InjectDependencies(this);
        _fontResource = _cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _font = new VectorFont(_fontResource, BaseFontSize);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // ── 2D path ──────────────────────────────────────────────────────
        if (_entMan.TryGetComponent<CEGeneratingProceduralDungeonComponent>(args.MapUid, out var dun)
            && dun.Rooms.Count > 0)
        {
            if (args.Space == OverlaySpace.WorldSpace)
                DrawWorld(in args, dun);
            else if (args.Space == OverlaySpace.ScreenSpace)
                DrawScreen(in args, dun);
            return;
        }

        // ── 3D path ──────────────────────────────────────────────────────
        if (TryFind3DComp(args.MapUid, out var dun3d, out var zIndex) && dun3d is not null && dun3d.Rooms.Count > 0)
        {
            if (args.Space == OverlaySpace.WorldSpace)
                DrawWorld3D(in args, dun3d, zIndex);
            else if (args.Space == OverlaySpace.ScreenSpace)
                DrawScreen3D(in args, dun3d, zIndex);
        }

        // ── World chunk grid ───────────────────────────────────────────────
        if (_entMan.TryGetComponent<CEZLevelMapComponent>(args.MapUid, out var zMap) &&
            _entMan.TryGetComponent<CEWorldComponent>(zMap.NetworkUid, out var world) &&
            world.ChunkMap.Count > 0)
        {
            var chunkZ = zMap.Depth; // CEWorldComponent.ChunkHeight == 1, so chunkZ == Depth
            if (args.Space == OverlaySpace.WorldSpace)
                DrawWorldChunks(in args, world.ChunkMap, chunkZ);
            else if (args.Space == OverlaySpace.ScreenSpace)
                DrawScreenChunks(in args, world.ChunkMap, chunkZ);
        }
    }

    // ── Dungeon draw ───────────────────────────────────────────────────────

    private void DrawWorld(in OverlayDrawArgs args, CEGeneratingProceduralDungeonComponent comp)
    {
        var handle = args.WorldHandle;

        foreach (var room in comp.Rooms)
        {
            var roomTypeProto = room.RoomType != null && _proto.Resolve(room.RoomType.Value, out var roomType)
                ? roomType
                : null;
            var fillColor = roomTypeProto?.DebugFillColor ?? DefaultFillColor;
            var borderColor = roomTypeProto?.DebugBorderColor ?? DefaultBorderColor;

            var box = new Box2(
                room.Position.X,
                room.Position.Y,
                room.Position.X + room.Size.X,
                room.Position.Y + room.Size.Y);

            handle.DrawRect(box, fillColor);

            var tl = new Vector2(box.Left, box.Top);
            var tr = new Vector2(box.Right, box.Top);
            var bl = new Vector2(box.Left, box.Bottom);
            var br = new Vector2(box.Right, box.Bottom);

            handle.DrawLine(tl, tr, borderColor);
            handle.DrawLine(tr, br, borderColor);
            handle.DrawLine(br, bl, borderColor);
            handle.DrawLine(bl, tl, borderColor);
        }

        foreach (var conn in comp.Connections)
        {
            if (conn.RoomA < 0 || conn.RoomA >= comp.Rooms.Count ||
                conn.RoomB < 0 || conn.RoomB >= comp.Rooms.Count)
                continue;

            var roomA = comp.Rooms[conn.RoomA];
            var roomB = comp.Rooms[conn.RoomB];

            var centerA = new Vector2(
                roomA.Position.X + roomA.Size.X / 2f,
                roomA.Position.Y + roomA.Size.Y / 2f);

            var centerB = new Vector2(
                roomB.Position.X + roomB.Size.X / 2f,
                roomB.Position.Y + roomB.Size.Y / 2f);

            handle.DrawLine(centerA, centerB, ConnectionColor);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, CEGeneratingProceduralDungeonComponent comp)
    {
        var handle = args.ScreenHandle;
        var viewport = args.ViewportControl;
        if (viewport == null)
            return;

        var zoom = args.Viewport.Eye?.Zoom ?? Vector2.One;
        var zoomFactor = Math.Max(zoom.X, zoom.Y);
        var scaledSize = Math.Max(6, (int)(BaseFontSize / zoomFactor));
        var font = scaledSize == BaseFontSize
            ? _font
            : new VectorFont(_fontResource, scaledSize);

        foreach (var room in comp.Rooms)
        {
            var worldCenter = new Vector2(
                room.Position.X + room.Size.X / 2f,
                room.Position.Y + room.Size.Y / 2f);

            var screenPos = viewport.WorldToScreen(worldCenter);

            var label = $"#{room.Index} [{room.RoomType}]\n" +
                        $"grid: {room.GridCoord}\n" +
                        $"pos: {room.Position}\n" +
                        $"size: {room.Size.X}x{room.Size.Y}\n" +
                        $"proto: {room.RoomProtoId ?? "none"}\n" +
                        $"rot: {Math.Round(room.Rotation.Theta * 180 / Math.PI)}°";

            handle.DrawString(font, screenPos, label);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // 3D drawing — pseudo-isometric cube per room
    // ════════════════════════════════════════════════════════════════════

    private void DrawWorld3D(in OverlayDrawArgs args, CEGeneratingProceduralDungeon3DComponent comp, int zIndex)
    {
        var handle = args.WorldHandle;

        foreach (var room in comp.Rooms)
        {
            // Room at GridCoord.Z=n occupies z-levels [n*Height .. (n+1)*Height - 1].
            var zStart = room.GridCoord.Z * room.Height;
            var zEnd = zStart + room.Height - 1;
            if (zIndex < zStart || zIndex > zEnd)
                continue;

            var roomTypeProto = room.RoomType != null && _proto.Resolve(room.RoomType.Value, out var rt) ? rt : null;
            var baseColor = roomTypeProto?.Color ?? Color.Gray;

            var box = new Box2(room.Position.X, room.Position.Y,
                room.Position.X + room.Size.X, room.Position.Y + room.Size.Y);

            handle.DrawRect(box, baseColor.WithAlpha(0.30f));
        }

        // Camera rotation inverse — same formula as CEClientZLevelsSystem.OnEyeOffset.
        var camInverse = -(args.Viewport.Eye?.Rotation ?? Angle.Zero);

        foreach (var conn in comp.Connections)
        {
            if ((uint)conn.RoomA >= (uint)comp.Rooms.Count || (uint)conn.RoomB >= (uint)comp.Rooms.Count)
                continue;

            var a = comp.Rooms[conn.RoomA];
            var b = comp.Rooms[conn.RoomB];
            var centerA = new Vector2(a.Position.X + a.Size.X / 2f, a.Position.Y + a.Size.Y / 2f);
            var centerB = new Vector2(b.Position.X + b.Size.X / 2f, b.Position.Y + b.Size.Y / 2f);

            var aZStart = a.GridCoord.Z * a.Height;
            var aZEnd = aZStart + a.Height - 1;
            var bZStart = b.GridCoord.Z * b.Height;
            var bZEnd = bZStart + b.Height - 1;

            if (a.GridCoord.Z == b.GridCoord.Z)
            {
                // Flat connection within the same z-group.
                if (zIndex >= aZStart && zIndex <= aZEnd)
                    handle.DrawLine(centerA, centerB, ConnectionColor);
            }
            else
            {
                // Vertical connection: ceiling of lower room → floor of upper room.
                // Drawn on exactly two z-levels so the line is visible from both sides.
                var lowerCenter = a.GridCoord.Z < b.GridCoord.Z ? centerA : centerB;
                var upperCenter = a.GridCoord.Z < b.GridCoord.Z ? centerB : centerA;
                var lowerZEnd = a.GridCoord.Z < b.GridCoord.Z ? aZEnd : bZEnd;
                var upperZStart = a.GridCoord.Z < b.GridCoord.Z ? bZStart : aZStart;

                if (zIndex == lowerZEnd)
                {
                    // On the ceiling of the lower room — line goes up to where upper floor appears.
                    var toUpper = upperCenter + camInverse.RotateVec(
                        new Vector2(0, (upperZStart - zIndex) * CESharedZLevelsSystem.ZLevelOffset));
                    handle.DrawLine(lowerCenter, toUpper, ConnectionColor);
                }
                else if (zIndex == upperZStart)
                {
                    // On the floor of the upper room — line comes from where lower ceiling appears.
                    var fromLower = lowerCenter + camInverse.RotateVec(
                        new Vector2(0, (lowerZEnd - zIndex) * CESharedZLevelsSystem.ZLevelOffset));
                    handle.DrawLine(fromLower, upperCenter, ConnectionColor);
                }
            }
        }
    }

    private void DrawScreen3D(in OverlayDrawArgs args, CEGeneratingProceduralDungeon3DComponent comp, int zIndex)
    {
        var handle = args.ScreenHandle;
        var viewport = args.ViewportControl;
        if (viewport == null)
            return;

        var zoom = args.Viewport.Eye?.Zoom ?? Vector2.One;
        var zoomFactor = Math.Max(zoom.X, zoom.Y);
        var scaledSize = Math.Max(6, (int)(BaseFontSize / zoomFactor));
        var font = scaledSize == BaseFontSize ? _font : new VectorFont(_fontResource, scaledSize);

        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];
            var zStart = room.GridCoord.Z * room.Height;
            var zEnd = zStart + room.Height - 1;
            if (zIndex < zStart || zIndex > zEnd)
                continue;

            var worldCenter = new Vector2(room.Position.X + room.Size.X / 2f, room.Position.Y + room.Size.Y / 2f);
            var screenPos = viewport.WorldToScreen(worldCenter);

            var label = $"#{i} [{room.RoomType}]\n" +
                        $"grid: {room.GridCoord}\n" +
                        $"pos: {room.Position}\n" +
                        $"size: {room.Size.X}x{room.Size.Y}x{room.Height}";

            handle.DrawString(font, screenPos, label);
        }
    }

    private bool TryFind3DComp(
        EntityUid mapUid,
        out CEGeneratingProceduralDungeon3DComponent? comp,
        out int zIndex)
    {
        // CEZLevelMapComponent is automatically added to every map in a z-network
        // and carries NetworkUid + Depth — no restricted dictionary access needed.
        if (_entMan.TryGetComponent<CEZLevelMapComponent>(mapUid, out var mapComp) &&
            _entMan.TryGetComponent(mapComp.NetworkUid, out comp))
        {
            zIndex = mapComp.Depth;
            return true;
        }

        comp = default!;
        zIndex = 0;
        return false;
    }

    // ── World chunk draw ───────────────────────────────────────────────────

    private void DrawWorldChunks(
        in OverlayDrawArgs args,
        Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> chunkMap,
        int chunkZ)
    {
        var eyePos = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var cx = (int)MathF.Floor(eyePos.X / CEWorldComponent.ChunkSize);
        var cy = (int)MathF.Floor(eyePos.Y / CEWorldComponent.ChunkSize);
        var radius = (int)MathF.Ceiling(CEWorldComponent.LoadRadiusChunks * 3);

        var handle = args.WorldHandle;
        foreach (var (pos, typeId) in chunkMap)
        {
            if (pos.Z != chunkZ) continue;
            if (Math.Abs(pos.X - cx) > radius || Math.Abs(pos.Y - cy) > radius) continue;

            var color = _proto.TryIndex(typeId, out var chunkType) ? chunkType.DebugColor : Color.Gray;
            var box = new Box2(
                pos.X * CEWorldComponent.ChunkSize,
                pos.Y * CEWorldComponent.ChunkSize,
                (pos.X + 1) * CEWorldComponent.ChunkSize,
                (pos.Y + 1) * CEWorldComponent.ChunkSize);

            handle.DrawRect(box, color.WithAlpha(0.25f));
            handle.DrawLine(box.TopLeft, box.TopRight, color);
            handle.DrawLine(box.TopRight, box.BottomRight, color);
            handle.DrawLine(box.BottomRight, box.BottomLeft, color);
            handle.DrawLine(box.BottomLeft, box.TopLeft, color);
        }
    }

    private void DrawScreenChunks(
        in OverlayDrawArgs args,
        Dictionary<Vector3i, ProtoId<CEWorldChunkTypePrototype>> chunkMap,
        int chunkZ)
    {
        var viewport = args.ViewportControl;
        if (viewport == null)
            return;

        var eyePos = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var cx = (int)MathF.Floor(eyePos.X / CEWorldComponent.ChunkSize);
        var cy = (int)MathF.Floor(eyePos.Y / CEWorldComponent.ChunkSize);
        var radius = (int)MathF.Ceiling(CEWorldComponent.LoadRadiusChunks * 3);

        var zoom = args.Viewport.Eye?.Zoom ?? Vector2.One;
        var zoomFactor = Math.Max(zoom.X, zoom.Y);
        var scaledSize = Math.Max(6, (int)(BaseFontSize / zoomFactor));
        var font = scaledSize == BaseFontSize
            ? _font
            : new VectorFont(_fontResource, scaledSize);

        var handle = args.ScreenHandle;
        foreach (var (pos, typeId) in chunkMap)
        {
            if (pos.Z != chunkZ) continue;
            if (Math.Abs(pos.X - cx) > radius || Math.Abs(pos.Y - cy) > radius) continue;

            var worldCenter = new Vector2(
                (pos.X + 0.5f) * CEWorldComponent.ChunkSize,
                (pos.Y + 0.5f) * CEWorldComponent.ChunkSize);

            var screenPos = viewport.WorldToScreen(worldCenter);
            handle.DrawString(font, screenPos, $"{typeId.Id}\n({pos.X},{pos.Y})");
        }
    }
}