using System.Numerics;
using Content.Shared._CE.Procedural;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client._CE.Procedural;

/// <summary>
/// Debug overlay that visualizes <see cref="CEGeneratingProceduralDungeonComponent"/> data:
/// draws coloured rectangles for each abstract room and lines for room connections.
/// </summary>
public sealed class CEProceduralGenerationOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    private readonly Font _font;

    /// <summary>
    /// Fill colours keyed by room type.
    /// </summary>
    private static readonly Dictionary<CEProceduralRoomType, Color> TypeFillColors = new()
    {
        { CEProceduralRoomType.General,   Color.Gray.WithAlpha(0.08f) },
        { CEProceduralRoomType.Exit,      Color.Red.WithAlpha(0.12f) },
        { CEProceduralRoomType.Entrance,  Color.Green.WithAlpha(0.12f) },
        { CEProceduralRoomType.Blessing,  Color.Yellow.WithAlpha(0.12f) },
    };

    /// <summary>
    /// Border colours keyed by room type.
    /// </summary>
    private static readonly Dictionary<CEProceduralRoomType, Color> TypeBorderColors = new()
    {
        { CEProceduralRoomType.General,   Color.Gray.WithAlpha(0.8f) },
        { CEProceduralRoomType.Exit,      Color.Red.WithAlpha(0.8f) },
        { CEProceduralRoomType.Entrance,  Color.Green.WithAlpha(0.8f) },
        { CEProceduralRoomType.Blessing,  Color.Yellow.WithAlpha(0.8f) },
    };

    private static readonly Color ConnectionColor = Color.White.WithAlpha(0.6f);

    public CEProceduralGenerationOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent<CEGeneratingProceduralDungeonComponent>(args.MapUid, out var dun))
            return;

        if (dun.Rooms.Count == 0)
            return;

        if (args.Space == OverlaySpace.WorldSpace)
            DrawWorld(in args, dun);
        else if (args.Space == OverlaySpace.ScreenSpace)
            DrawScreen(in args, dun);
    }

    private void DrawWorld(in OverlayDrawArgs args, CEGeneratingProceduralDungeonComponent comp)
    {
        var handle = args.WorldHandle;

        // Draw room rectangles.
        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];
            var fillColor = TypeFillColors.GetValueOrDefault(room.RoomType, Color.Gray.WithAlpha(0.08f));
            var borderColor = TypeBorderColors.GetValueOrDefault(room.RoomType, Color.Gray.WithAlpha(0.8f));

            var box = new Box2(
                room.Position.X,
                room.Position.Y,
                room.Position.X + room.Size.X,
                room.Position.Y + room.Size.Y);

            handle.DrawRect(box, fillColor);

            // Border.
            var tl = new Vector2(box.Left, box.Top);
            var tr = new Vector2(box.Right, box.Top);
            var bl = new Vector2(box.Left, box.Bottom);
            var br = new Vector2(box.Right, box.Bottom);

            handle.DrawLine(tl, tr, borderColor);
            handle.DrawLine(tr, br, borderColor);
            handle.DrawLine(br, bl, borderColor);
            handle.DrawLine(bl, tl, borderColor);
        }

        // Draw connection lines between room centres.
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

            handle.DrawString(_font, screenPos, label);
        }
    }
}
