using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Tiles;

// CrystallEdge: DoAfter event for placing floor tiles with a delay
[Serializable, NetSerializable]
public sealed partial class FloorTileDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    [DataField]
    public NetEntity? TargetGrid;

    [DataField]
    public ushort TileId;

    [DataField]
    public SoundSpecifier PlaceSound = new SoundPathSpecifier("/Audio/Items/genhit.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.125f),
    };

    [DataField]
    public float Offset = 0f;

    public override DoAfterEvent Clone() => this;
}
// CrystallEdge end
