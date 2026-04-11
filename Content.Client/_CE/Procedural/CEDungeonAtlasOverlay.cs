using System.Numerics;
using Content.Shared._CE.Procedural;
using Content.Shared._CE.ZLevels.Mapping.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Procedural;

/// <summary>
/// Draws rectangles showing the size and position of each <see cref="CEDungeonRoom3DPrototype"/>
/// that uses the selected <see cref="CEZLevelMapPrototype"/> as its atlas source.
/// </summary>
public sealed class CEDungeonAtlasOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    /// <summary>
    /// The zMap prototype ID to visualize. Null means no rooms are drawn.
    /// </summary>
    public ProtoId<CEZLevelMapPrototype>? ZMapProtoId;

    private readonly Font _font;

    // Colors for cycling through rooms.
    private static readonly Color[] RoomColors =
    [
        Color.Red.    WithAlpha(0.05f),
        Color.Blue.   WithAlpha(0.05f),
        Color.Green.  WithAlpha(0.05f),
        Color.Yellow. WithAlpha(0.05f),
        Color.Cyan.   WithAlpha(0.05f),
        Color.Magenta.WithAlpha(0.05f),
        Color.Orange. WithAlpha(0.05f),
    ];

    private static readonly Color[] RoomBorderColors =
    [
        Color.Red.    WithAlpha(0.8f),
        Color.Blue.   WithAlpha(0.8f),
        Color.Green.  WithAlpha(0.8f),
        Color.Yellow. WithAlpha(0.8f),
        Color.Cyan.   WithAlpha(0.8f),
        Color.Magenta.WithAlpha(0.8f),
        Color.Orange. WithAlpha(0.8f),
    ];

    public CEDungeonAtlasOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ZMapProtoId == null)
            return;

        var rooms = GetMatchingRooms(ZMapProtoId.Value);
        if (rooms.Count == 0)
            return;

        if (args.Space == OverlaySpace.WorldSpace)
            DrawWorld(in args, rooms);
        else if (args.Space == OverlaySpace.ScreenSpace)
            DrawScreen(in args, rooms);
    }

    private void DrawWorld(in OverlayDrawArgs args, List<CEDungeonRoom3DPrototype> rooms)
    {
        var handle = args.WorldHandle;

        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var colorIndex = i % RoomColors.Length;
            var fillColor = RoomColors[colorIndex];
            var borderColor = RoomBorderColors[colorIndex];

            // Room bounds in tile coordinates (offset is bottom-left corner).
            var box = new Box2(
                room.Offset.X,
                room.Offset.Y,
                room.Offset.X + room.Size.X,
                room.Offset.Y + room.Size.Y);

            // Draw filled rect.
            handle.DrawRect(box, fillColor);

            // Draw border lines.
            var tl = new Vector2(box.Left, box.Top);
            var tr = new Vector2(box.Right, box.Top);
            var bl = new Vector2(box.Left, box.Bottom);
            var br = new Vector2(box.Right, box.Bottom);

            handle.DrawLine(tl, tr, borderColor);
            handle.DrawLine(tr, br, borderColor);
            handle.DrawLine(br, bl, borderColor);
            handle.DrawLine(bl, tl, borderColor);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, List<CEDungeonRoom3DPrototype> rooms)
    {
        var handle = args.ScreenHandle;
        var viewport = args.ViewportControl;
        if (viewport == null)
            return;

        foreach (var room in rooms)
        {
            // Place label at center of the room in world space, then project to screen.
            var worldCenter = new Vector2(
                room.Offset.X + room.Size.X / 2f,
                room.Offset.Y + room.Size.Y / 2f);
            var screenPos = viewport.WorldToScreen(worldCenter);

            var label = $"{room.ID} \n" +
                        $"size: {room.Size.X}x{room.Size.Y} \n" +
                        $"height: {room.Height} \n" +
                        $"offset: {room.Offset}";

            handle.DrawString(_font, screenPos, label);
        }
    }

    private List<CEDungeonRoom3DPrototype> GetMatchingRooms(ProtoId<CEZLevelMapPrototype> zMapId)
    {
        var result = new List<CEDungeonRoom3DPrototype>();
        foreach (var room in _proto.EnumeratePrototypes<CEDungeonRoom3DPrototype>())
        {
            if (room.ZLevelMap == zMapId)
                result.Add(room);
        }

        return result;
    }
}
