// Herringbone tile-placement layout ported from the public-domain / MIT-licensed
// stb_herringbone_wang_tile.h by Sean Barrett (nothings.org).
//   Source:   https://github.com/nothings/stb/blob/master/stb_herringbone_wang_tile.h
//   Articles: https://nothings.org/gamedev/herringbone/herringbone_tiles.html
//             https://nothings.org/gamedev/herringbone/more_herringbone_tiles.html
// stb is dual-licensed: public domain (Unlicense) OR MIT, at your option.
// Only the herringbone tile-placement layout is used here (reduced to a closed form); no stb source is
// copied verbatim. The Wang edge-color matching from stb is NOT used — rooms are placed flush and each
// domino's room is chosen by a stateless position hash, so generation is identical regardless of load order.

using Robust.Shared.Maths;

namespace Content.Shared._CE.WorldGen;

/// <summary>
/// Orientation of a herringbone domino: it spans two chunk cells either along X or along Y.
/// </summary>
public enum CEHerringboneOrientation : byte
{
    /// <summary>
    /// Domino spans X: cells (anchor) and (anchor + (1, 0)).
    /// </summary>
    Horizontal,

    /// <summary>
    /// Domino spans Y: cells (anchor) and (anchor + (0, 1)).
    /// </summary>
    Vertical,
}

/// <summary>
/// A herringbone domino: its anchor (lower-left / bottom cell, in absolute chunk coords) and orientation.
/// Covers exactly two chunk cells.
/// </summary>
public readonly record struct CEHerringboneDomino(Vector2i Anchor, CEHerringboneOrientation Orientation)
{
    /// <summary>
    /// First cell of the domino (the anchor; left for horizontal, bottom for vertical).
    /// </summary>
    public Vector2i CellA => Anchor;

    /// <summary>
    /// Second cell of the domino (right for horizontal, top for vertical).
    /// </summary>
    public Vector2i CellB => Orientation == CEHerringboneOrientation.Horizontal
        ? Anchor + new Vector2i(1, 0)
        : Anchor + new Vector2i(0, 1);

    /// <summary>
    /// The cell for the given half (0 == anchor, 1 == the other).
    /// </summary>
    public Vector2i Cell(int half) => half == 0 ? CellA : CellB;
}

/// <summary>
/// Pure, deterministic herringbone lattice over the chunk plane: a domino is two chunk cells laid out in a
/// herringbone (basket-weave) tiling so chunk seams never form continuous lines. The layout is the canonical
/// stb_herringbone_wang_tile one, reduced to a closed form (see <see cref="Classify"/>).
///
/// Room selection is a pure function of position + world seed (stateless hashing), so generation is identical
/// regardless of chunk load order / approach direction, and works for an unbounded world.
/// </summary>
public static class CEHerringboneLattice
{
    /// <summary>
    /// Classifies a chunk into its domino (anchor in absolute chunk coords, orientation) and which half of
    /// that domino it is (0 == anchor cell, 1 == other cell).
    ///
    /// The partition is z-invariant: <c>r = FloorMod(cx - cy, 4)</c> does not depend on cz, so every
    /// z-level uses the exact same herringbone partition and dominoes stack vertically aligned. This lets
    /// two-story connection rooms occupy the same domino footprint on two adjacent floors. The within-floor
    /// herringbone (so chunk seams never form continuous lines) is preserved; only the per-z variation is
    /// dropped. The <paramref name="cz"/> parameter is kept for API stability and is unused here — room
    /// variety per floor still comes from <see cref="RoomSeed"/>, which mixes cz.
    /// </summary>
    public static (CEHerringboneDomino Domino, int Half) Classify(int cx, int cy, int cz)
    {
        var sx = cx;
        var sy = cy;
        var r = FloorMod(sx - sy, 4);

        Vector2i anchor;
        CEHerringboneOrientation orient;
        int half;

        switch (r)
        {
            case 0: // horizontal, left half
                anchor = new Vector2i(sx, sy);
                orient = CEHerringboneOrientation.Horizontal;
                half = 0;
                break;
            case 1: // horizontal, right half
                anchor = new Vector2i(sx - 1, sy);
                orient = CEHerringboneOrientation.Horizontal;
                half = 1;
                break;
            case 2: // vertical, top half
                anchor = new Vector2i(sx, sy - 1);
                orient = CEHerringboneOrientation.Vertical;
                half = 1;
                break;
            default: // r == 3: vertical, bottom half
                anchor = new Vector2i(sx, sy);
                orient = CEHerringboneOrientation.Vertical;
                half = 0;
                break;
        }

        return (new CEHerringboneDomino(anchor, orient), half);
    }

    /// <summary>
    /// Deterministic per-domino room-selection seed. Depends on the domino (anchor + orientation), the
    /// chunk-z layer (<paramref name="cz"/>) and the world seed — never on half — so both halves at the
    /// same z-level pick the same room. Including cz ensures vertically-stacked dominoes get different rooms
    /// even when they share an anchor (which can happen at z-level multiples of 4 due to the X-shift period).
    /// </summary>
    public static int RoomSeed(CEHerringboneDomino domino, int cz, int seed)
    {
        var h = Hash((uint)seed ^ 0x1234567u, (uint)domino.Anchor.X, (uint)domino.Anchor.Y, (byte)domino.Orientation);
        return (int)Mix(h, (uint)cz);
    }

    /// <summary>
    /// Floored modulo (result always in [0, m) for positive m).
    /// </summary>
    public static int FloorMod(int a, int m)
    {
        var r = a % m;
        return r < 0 ? r + m : r;
    }

    private static uint Hash(uint seed, uint a, uint b, uint c)
    {
        var h = seed;
        h = Mix(h, a);
        h = Mix(h, b);
        h = Mix(h, c);

        // Final avalanche.
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        return h;
    }

    private static uint Mix(uint h, uint v)
    {
        unchecked
        {
            h ^= v;
            h *= 0x9E3779B1u;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            return h;
        }
    }
}
