using System.Numerics;
using Content.Shared._CE.Procedural;
using Robust.Client.GameObjects;
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

    private static readonly Color[] RoomColors =
    [
        Color.Red.WithAlpha(0.08f),
        Color.Blue.WithAlpha(0.08f),
        Color.Green.WithAlpha(0.08f),
        Color.Yellow.WithAlpha(0.08f),
        Color.Cyan.WithAlpha(0.08f),
        Color.Magenta.WithAlpha(0.08f),
        Color.Orange.WithAlpha(0.08f),
    ];

    private static readonly Color[] RoomBorderColors =
    [
        Color.Red.WithAlpha(0.8f),
        Color.Blue.WithAlpha(0.8f),
        Color.Green.WithAlpha(0.8f),
        Color.Yellow.WithAlpha(0.8f),
        Color.Cyan.WithAlpha(0.8f),
        Color.Magenta.WithAlpha(0.8f),
        Color.Orange.WithAlpha(0.8f),
    ];

    private static readonly Color ConnectionColor = Color.White.WithAlpha(0.6f);

    public CEProceduralGenerationOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entMan.EntityQueryEnumerator<CEGeneratingProceduralDungeonComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.Rooms.Count == 0)
                continue;

            if (args.Space == OverlaySpace.WorldSpace)
                DrawWorld(in args, comp);
            else if (args.Space == OverlaySpace.ScreenSpace)
                DrawScreen(in args, comp);
        }
    }

    private void DrawWorld(in OverlayDrawArgs args, CEGeneratingProceduralDungeonComponent comp)
    {
        var handle = args.WorldHandle;

        // Draw room rectangles.
        for (var i = 0; i < comp.Rooms.Count; i++)
        {
            var room = comp.Rooms[i];
            var colorIdx = i % RoomColors.Length;
            var fillColor = RoomColors[colorIdx];
            var borderColor = RoomBorderColors[colorIdx];

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

            var label = $"#{room.Index}\n" +
                        $"grid: {room.GridCoord}\n" +
                        $"pos: {room.Position}\n" +
                        $"size: {room.Size.X}x{room.Size.Y}\n" +
                        $"proto: {room.RoomProtoId ?? "none"}\n" +
                        $"rot: {Math.Round(room.Rotation.Theta * 180 / Math.PI)}°";

            handle.DrawString(_font, screenPos, label);
        }
    }
}
