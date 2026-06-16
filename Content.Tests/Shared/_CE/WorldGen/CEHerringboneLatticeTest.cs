using System.Collections.Generic;
using Content.Shared._CE.WorldGen;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._CE.WorldGen;

[TestFixture]
[TestOf(typeof(CEHerringboneLattice))]
public sealed class CEHerringboneLatticeTest
{
    private const int Seed = 1337;

    private static IEnumerable<(int X, int Y, int Z)> Chunks()
    {
        for (var z = -3; z <= 3; z++)
        for (var x = -24; x <= 24; x++)
        for (var y = -24; y <= 24; y++)
            yield return (x, y, z);
    }

    /// <summary>
    /// The domino a chunk is classified into must actually contain that chunk at the reported half.
    /// </summary>
    [Test]
    public void ClassifyContainsCell()
    {
        foreach (var (x, y, z) in Chunks())
        {
            var (domino, half) = CEHerringboneLattice.Classify(x, y, z);
            Assert.That(domino.Cell(half), Is.EqualTo(new Vector2i(x, y)),
                $"chunk ({x},{y},{z}) not contained by its own domino");
        }
    }

    /// <summary>
    /// Every chunk pairs with exactly one orthogonally-adjacent partner, and both cells agree on the same
    /// domino — i.e. the lattice is a clean 2-cell partition (no gaps, no overlaps) per z-layer.
    /// </summary>
    [Test]
    public void PartitionIsConsistent()
    {
        foreach (var (x, y, z) in Chunks())
        {
            var (domino, half) = CEHerringboneLattice.Classify(x, y, z);
            var partner = domino.Cell(1 - half);

            // Partner is orthogonally adjacent.
            var delta = partner - new Vector2i(x, y);
            Assert.That(System.Math.Abs(delta.X) + System.Math.Abs(delta.Y), Is.EqualTo(1),
                $"partner of ({x},{y},{z}) is not orthogonally adjacent");

            // Partner classifies into the same domino, as the other half.
            var (partnerDomino, partnerHalf) = CEHerringboneLattice.Classify(partner.X, partner.Y, z);
            Assert.That(partnerDomino, Is.EqualTo(domino), $"partner of ({x},{y},{z}) disagrees on domino");
            Assert.That(partnerHalf, Is.EqualTo(1 - half), $"partner of ({x},{y},{z}) disagrees on half");
        }
    }

    /// <summary>
    /// Both orientations occur (it really is herringbone, not all one way).
    /// </summary>
    [Test]
    public void BothOrientationsOccur()
    {
        var horizontal = false;
        var vertical = false;

        foreach (var (x, y, z) in Chunks())
        {
            var (domino, _) = CEHerringboneLattice.Classify(x, y, z);
            if (domino.Orientation == CEHerringboneOrientation.Horizontal)
                horizontal = true;
            else
                vertical = true;
        }

        Assert.That(horizontal, "no horizontal dominoes");
        Assert.That(vertical, "no vertical dominoes");
    }

    /// <summary>
    /// Classification is a pure function (same inputs → same output).
    /// </summary>
    [Test]
    public void ClassifyIsPure()
    {
        foreach (var (x, y, z) in Chunks())
        {
            Assert.That(CEHerringboneLattice.Classify(x, y, z), Is.EqualTo(CEHerringboneLattice.Classify(x, y, z)));
        }
    }

    /// <summary>
    /// Both halves of a domino derive the same room-selection seed, so they pick the same room.
    /// </summary>
    [Test]
    public void RoomSeedAgreesAcrossHalves()
    {
        foreach (var (x, y, z) in Chunks())
        {
            var (domino, half) = CEHerringboneLattice.Classify(x, y, z);
            var partner = domino.Cell(1 - half);
            var (partnerDomino, _) = CEHerringboneLattice.Classify(partner.X, partner.Y, z);

            Assert.That(CEHerringboneLattice.RoomSeed(partnerDomino, Seed),
                Is.EqualTo(CEHerringboneLattice.RoomSeed(domino, Seed)),
                $"halves of domino at {domino.Anchor} disagree on room seed");
        }
    }
}
