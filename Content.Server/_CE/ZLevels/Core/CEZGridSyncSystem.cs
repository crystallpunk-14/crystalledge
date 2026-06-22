/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Synchronizes all grids in a z-network as a single rigid body around one stable anchor grid.
///
/// Geometry model: each network designates one <see cref="CEZGridNetworkComponent.AnchorGrid"/>
/// (static planet if present, otherwise the lowest <see cref="EntityUid"/> for determinism). Every
/// other grid stores its pose as <see cref="CEZGridComponent.NetworkOffset"/>/
/// <see cref="CEZGridComponent.NetworkRotation"/> relative to that anchor. The anchor is the single
/// source of truth — non-anchor grids are placed from anchor+offset each substep, eliminating drift
/// without averaging.
///
/// Membership changes are incremental: a newly linked grid snaps to the anchor (once), survivors are
/// never re-snapped, and when the anchor leaves the remaining grids are rebased onto a new anchor
/// in place (no movement).
/// </summary>
public sealed partial class CEZGridSyncSystem : VirtualController
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CEZLevelsSystem _zlevels = default!;
    [Dependency] private CEZGridConnectorSystem _connectorSystem = default!;

    [Dependency] private EntityQuery<CEZGridComponent> _gridCompQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _zGridNetworkQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapCompQuery = default!;

    private bool _inPhysicsTick;
    private bool _syncing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZGridComponent, CEGridAddedIntoZNetworkEvent>(OnGridLinked);
        SubscribeLocalEvent<CEZGridComponent, CEGridRemovedFromZNetworkEvent>(OnGridUnlinked);
        SubscribeLocalEvent<CEZGridComponent, MoveEvent>(OnGridMoved);
        SubscribeLocalEvent<CEZGridComponent, MassDataChangedEvent>(OnMassChanged);
    }
    private void OnGridLinked(Entity<CEZGridComponent> zGridEnt, ref CEGridAddedIntoZNetworkEvent ev)
    {
        if (!_zGridNetworkQuery.TryComp(ev.Network, out var net))
            return;

        zGridEnt.Comp.CachedFixturesMass = _physicsQuery.TryComp(zGridEnt.Owner, out var body)
            ? body.FixturesMass
            : 0f;

        if (net.Grids.Count == 1)
        {
            // First member becomes the anchor.
            zGridEnt.Comp.NetworkOffset = Vector2.Zero;
            zGridEnt.Comp.NetworkRotation = Angle.Zero;
            net.AnchorGrid = zGridEnt.Owner;
        }
        else if (IsStaticAnchor(zGridEnt.Owner) && net.AnchorGrid != zGridEnt.Owner)
        {
            // A planet joined an existing flying network: dock the whole network onto it as one rigid body.
            DockFlyingNetworkToStatic(net, zGridEnt.Owner);
        }
        else
        {
            // Make sure an anchor exists (recovery if it was invalidated), then snap only the new grid.
            EnsureAnchor(net);
            if (zGridEnt.Owner != net.AnchorGrid)
                SnapNewGridToAnchor(net, zGridEnt);
        }

        RecalculateNetworkCache((ev.Network, net));
    }

    private void OnGridUnlinked(Entity<CEZGridComponent> ent, ref CEGridRemovedFromZNetworkEvent ev)
    {
        ent.Comp.NetworkOffset = Vector2.Zero;
        ent.Comp.NetworkRotation = Angle.Zero;
        ent.Comp.CachedFixturesMass = 0f;

        if (!_zGridNetworkQuery.TryComp(ev.Network, out var net))
            return;

        // If the departing grid was the anchor, rebase the survivors onto a new one (in place, no move).
        if (net.AnchorGrid == ent.Owner)
        {
            net.AnchorGrid = EntityUid.Invalid;
            var newAnchor = ChooseAnchor(net);
            if (newAnchor.IsValid())
                RebaseToAnchor(net, newAnchor);
        }

        RecalculateNetworkCache((ev.Network, net));
    }
    private void OnGridMoved(Entity<CEZGridComponent> ent, ref MoveEvent ev)
    {
        if (_syncing || _inPhysicsTick)
            return;

        if ((ev.NewPosition.Position - ev.OldPosition.Position).LengthSquared() < 1e-9f
            && Math.Abs((ev.NewRotation - ev.OldRotation).Theta) < 1e-6)
            return;

        if (!_zlevels.TryGetGridNetwork(ent.Owner, out var network) || network.Comp.Grids.Count < 2)
            return;

        EnsureAnchor(network.Comp);

        if (network.Comp.HasStaticAnchor)
        {
            // Locked to a planet: any member that is shoved gets pinned back to anchor+offset.
            if (!network.Comp.AnchorGrid.IsValid())
                return;
            var (sPos, sRot) = GetGridPose(network.Comp.AnchorGrid);
            _syncing = true;
            ApplyAnchorToGrid(ent.Owner, ent.Comp, sPos, sRot);
            _syncing = false;
            return;
        }

        // Flying network: the moved grid drives, the rest follow its derived anchor frame.
        var (anchorPos, anchorRot) = GetAnchorPoseFromGrid(ent.Owner);
        _syncing = true;
        foreach (var other in network.Comp.Grids)
        {
            if (other == ent.Owner || !_gridCompQuery.TryComp(other, out var otherComp))
                continue;
            ApplyAnchorToGrid(other, otherComp, anchorPos, anchorRot);
        }
        _syncing = false;
    }

    private void OnMassChanged(Entity<CEZGridComponent> ent, ref MassDataChangedEvent args)
    {
        ent.Comp.CachedFixturesMass = _physicsQuery.TryComp(ent.Owner, out var body)
            ? body.FixturesMass
            : 0f;

        if (_zlevels.TryGetGridNetwork(ent.Owner, out var network))
            RecalculateNetworkCache(network);
    }

    // === Predicates / math helpers ===

    private bool IsStaticAnchor(EntityUid gridUid)
    {
        return _mapCompQuery.HasComponent(gridUid)
               || _physicsQuery.TryComp(gridUid, out var body)
               && !IsMoveable(body);
    }

    private static bool IsMoveable(PhysicsComponent body)
    {
        return (body.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) != 0x0;
    }

    private static Vector2 SnapPosition(Vector2 v, float tileSize)
    {
        return new Vector2(MathF.Round(v.X / tileSize) * tileSize,
            MathF.Round(v.Y / tileSize) * tileSize);
    }

    private static Angle SnapAngle(Angle a)
    {
        return Angle.FromDegrees(Math.Round(a.Degrees / 90.0) * 90.0);
    }

    private (Vector2 Pos, Angle Rot) GetGridPose(EntityUid grid)
    {
        return (_transform.GetWorldPosition(grid), _transform.GetWorldRotation(grid));
    }

    // Derives the virtual network anchor pose from one grid's world transform + its stored offset.
    // With consistent offsets every grid yields (almost) the same anchor; averaging removes the drift.
    private (Vector2 Pos, Angle Rot) GetAnchorPoseFromGrid(EntityUid grid)
    {
        var (pos, rot) = GetGridPose(grid);
        if (!_gridCompQuery.TryComp(grid, out var comp))
            return (pos, rot);

        var anchorRot = rot - comp.NetworkRotation;
        return (pos - anchorRot.RotateVec(comp.NetworkOffset), anchorRot);
    }

    private void ApplyAnchorToGrid(EntityUid gridUid, CEZGridComponent comp, Vector2 anchorPos, Angle anchorRot)
    {
        _transform.SetWorldPositionRotation(gridUid,
            anchorPos + anchorRot.RotateVec(comp.NetworkOffset),
            anchorRot + comp.NetworkRotation);
    }

    // === Anchor selection / cache ===

    /// <summary>Deterministic anchor: a static anchor takes priority, otherwise the lowest EntityUid.</summary>
    private EntityUid ChooseAnchor(CEZGridNetworkComponent net)
    {
        var best = EntityUid.Invalid;
        var bestStatic = false;

        foreach (var g in net.Grids)
        {
            var isStatic = IsStaticAnchor(g);
            if (!best.IsValid()
                || (isStatic && !bestStatic)
                || (isStatic == bestStatic && g.Id < best.Id))
            {
                best = g;
                bestStatic = isStatic;
            }
        }

        return best;
    }

    private bool HasValidAnchor(CEZGridNetworkComponent net)
    {
        return net.AnchorGrid.IsValid() && net.Grids.Contains(net.AnchorGrid);
    }

    /// <summary>Ensures the network has a valid anchor, rebasing every member onto it (in place) if it changed.</summary>
    private void EnsureAnchor(CEZGridNetworkComponent net)
    {
        if (HasValidAnchor(net))
            return;

        var anchor = ChooseAnchor(net);
        if (anchor.IsValid())
            RebaseToAnchor(net, anchor);
    }

    /// <summary>
    /// Re-expresses every grid's stored pose relative to <paramref name="newAnchor"/>'s current world
    /// transform. Pure bookkeeping — nothing is moved, so survivors keep their exact world poses.
    /// </summary>
    private void RebaseToAnchor(CEZGridNetworkComponent net, EntityUid newAnchor)
    {
        net.AnchorGrid = newAnchor;
        var (anchorPos, anchorRot) = GetGridPose(newAnchor);

        foreach (var g in net.Grids)
        {
            if (!_gridCompQuery.TryComp(g, out var comp))
                continue;

            if (g == newAnchor)
            {
                comp.NetworkOffset = Vector2.Zero;
                comp.NetworkRotation = Angle.Zero;
                continue;
            }

            var (gPos, gRot) = GetGridPose(g);
            comp.NetworkOffset = new Angle(-anchorRot.Theta).RotateVec(gPos - anchorPos);
            comp.NetworkRotation = gRot - anchorRot;
        }
    }

    /// <summary>
    /// Docks a flying network onto a static planet as one rigid body: the whole network is rotated by a
    /// single delta to the nearest 90° and translated once onto the planet tile grid, preserving the
    /// internal arrangement. The planet becomes the anchor; per-grid offsets are then exact.
    /// </summary>
    private void DockFlyingNetworkToStatic(CEZGridNetworkComponent net, EntityUid planet)
    {
        var (pPos, pRot) = GetGridPose(planet);
        net.AnchorGrid = planet;

        // Reference grid defines the network's current base rotation/pivot.
        var refGrid = EntityUid.Invalid;
        foreach (var g in net.Grids)
        {
            if (g != planet)
            {
                refGrid = g;
                break;
            }
        }

        if (!refGrid.IsValid())
            return;

        var pivot = _transform.GetWorldPosition(refGrid);
        var baseRot = _transform.GetWorldRotation(refGrid);
        var deltaRot = (pRot + SnapAngle(baseRot - pRot)) - baseRot;

        // Single tile-snap translation derived from the reference grid (rotates about itself → unchanged).
        var tileSize = _mapGridQuery.TryComp(refGrid, out var refMapGrid) ? refMapGrid.TileSize : 1f;
        var refLocal = new Angle(-pRot.Theta).RotateVec(pivot - pPos);
        var translation = pRot.RotateVec(SnapPosition(refLocal, tileSize) - refLocal);

        _syncing = true;
        foreach (var g in net.Grids)
        {
            if (g == planet || !_gridCompQuery.TryComp(g, out var comp))
                continue;

            var (gPos, gRot) = GetGridPose(g);
            var finalPos = pivot + deltaRot.RotateVec(gPos - pivot) + translation;
            var finalRot = gRot + deltaRot;

            comp.NetworkOffset = new Angle(-pRot.Theta).RotateVec(finalPos - pPos);
            comp.NetworkRotation = finalRot - pRot;
            _transform.SetWorldPositionRotation(g, finalPos, finalRot);

            if (_physicsQuery.TryComp(g, out var body) && IsMoveable(body))
            {
                PhysicsSystem.SetLinearVelocity(g, Vector2.Zero, body: body);
                PhysicsSystem.SetAngularVelocity(g, 0f, body: body);
            }
        }
        _syncing = false;
        _connectorSystem.MarkDirty();
    }

    private void RecalculateNetworkCache(Entity<CEZGridNetworkComponent> network)
    {
        var totalMass = 0f;
        var hasStatic = false;

        foreach (var g in network.Comp.Grids)
        {
            if (_gridCompQuery.TryComp(g, out var comp))
                totalMass += comp.CachedFixturesMass;
            if (IsStaticAnchor(g))
                hasStatic = true;
        }

        network.Comp.TotalCachedMass = totalMass;
        network.Comp.HasStaticAnchor = hasStatic;
    }

    /// <summary>Computes and (optionally) snaps a freshly linked grid's pose relative to the current anchor.</summary>
    private void SnapNewGridToAnchor(CEZGridNetworkComponent net, Entity<CEZGridComponent> grid)
    {
        var (anchorPos, anchorRot) = GetGridPose(net.AnchorGrid);
        var (world, worldRot) = GetGridPose(grid.Owner);

        var localOffset = new Angle(-anchorRot.Theta).RotateVec(world - anchorPos);
        var relRot = worldRot - anchorRot;

        // Static grids are never moved — record their exact pose.
        if (IsStaticAnchor(grid.Owner) || !_mapGridQuery.TryComp(grid.Owner, out var mapGrid))
        {
            grid.Comp.NetworkOffset = localOffset;
            grid.Comp.NetworkRotation = relRot;
            return;
        }

        var tileSize = mapGrid.TileSize;
        var snappedOffset = SnapPosition(localOffset, tileSize);
        var snappedRot = SnapAngle(relRot);

        // Always snap onto a static-anchored network; for flying merges only snap a small correction.
        var shouldSnap = net.HasStaticAnchor
                         || (snappedOffset - localOffset).Length() < tileSize
                         && Math.Abs((snappedRot - relRot).Theta) < Math.PI * 0.25;

        if (shouldSnap)
        {
            grid.Comp.NetworkOffset = snappedOffset;
            grid.Comp.NetworkRotation = snappedRot;
            _syncing = true;
            ApplyAnchorToGrid(grid.Owner, grid.Comp, anchorPos, anchorRot);
            _syncing = false;
            _connectorSystem.MarkDirty();
        }
        else
        {
            grid.Comp.NetworkOffset = localOffset;
            grid.Comp.NetworkRotation = relRot;
        }
    }

    // === Physics ===

    // Mass-weighted velocity consensus for a network (rigid body: v_i = vCom + ω×r_i).
    private void RunRigidBodyConsensus(CEZGridNetworkComponent net)
    {
        if (net.TotalCachedMass <= 0f)
            return;

        var com = Vector2.Zero;
        var totalMass = 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_gridCompQuery.TryComp(gUid, out var gComp) || gComp.CachedFixturesMass <= 0f)
                continue;
            com += _transform.GetWorldPosition(gUid) * gComp.CachedFixturesMass;
            totalMass += gComp.CachedFixturesMass;
        }

        if (totalMass <= 0f)
            return;

        com /= totalMass;

        var p = Vector2.Zero;
        var l = 0f;
        var iTotal = 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_physicsQuery.TryComp(gUid, out var body) || !_gridCompQuery.TryComp(gUid, out var gComp))
                continue;

            var mass = gComp.CachedFixturesMass;
            var r = _transform.GetWorldPosition(gUid) - com;

            if (mass > 0f)
            {
                p += body.LinearVelocity * mass;
                l += mass * (r.X * body.LinearVelocity.Y - r.Y * body.LinearVelocity.X);
            }

            if (body.Inertia > 0f)
            {
                l += body.Inertia * body.AngularVelocity;
                iTotal += body.Inertia + mass * (r.X * r.X + r.Y * r.Y);
            }
        }

        var vCom = p / totalMass;
        var omega = iTotal > 0f ? l / iTotal : 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_physicsQuery.TryComp(gUid, out var body) || !IsMoveable(body))
                continue;

            var r = _transform.GetWorldPosition(gUid) - com;
            var vTarget = vCom + new Vector2(-omega * r.Y, omega * r.X);

            if (!body.LinearVelocity.EqualsApprox(vTarget, 0.0001f))
                PhysicsSystem.SetLinearVelocity(gUid, vTarget, body: body);
            if (Math.Abs(body.AngularVelocity - omega) > 1e-4f)
                PhysicsSystem.SetAngularVelocity(gUid, omega, body: body);
        }
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        _inPhysicsTick = true;

        if (prediction)
            return;

        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count < 2)
                continue;

            if (net.HasStaticAnchor)
            {
                foreach (var gUid in net.Grids)
                {
                    if (!_physicsQuery.TryComp(gUid, out var body) || !IsMoveable(body))
                        continue;
                    if (body.LinearVelocity != Vector2.Zero)
                        PhysicsSystem.SetLinearVelocity(gUid, Vector2.Zero, body: body);
                    if (Math.Abs(body.AngularVelocity) > 1e-4f)
                        PhysicsSystem.SetAngularVelocity(gUid, 0f, body: body);
                }

                continue;
            }

            RunRigidBodyConsensus(net);
        }
    }

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        _inPhysicsTick = false;

        if (prediction)
            return;

        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count < 2)
                continue;

            // Static networks are kept still by the velocity-zeroing pass in UpdateBeforeSolve; we do
            // not reposition them here, so collisions read as real collisions rather than soft pins.
            if (net.HasStaticAnchor)
                continue;

            EnsureAnchor(net);
            if (!HasValidAnchor(net))
                continue;

            CorrectFlyingNetwork(net);
        }
    }

    // Flying network: redistribute momentum, then softly average each grid's derived anchor and
    // re-apply. This composes with external manipulation (griddrag) — a dragged grid pulls the
    // consensus toward itself instead of being snapped back.
    private void CorrectFlyingNetwork(CEZGridNetworkComponent net)
    {
        RunRigidBodyConsensus(net);

        var sumPos = Vector2.Zero;
        var sumRot = 0.0;
        var count = 0;

        foreach (var gUid in net.Grids)
        {
            if (!_gridCompQuery.HasComp(gUid))
                continue;
            var (aPos, aRot) = GetAnchorPoseFromGrid(gUid);
            sumPos += aPos;
            sumRot += aRot.Theta;
            count++;
        }

        if (count == 0)
            return;

        var avgPos = sumPos / count;
        var avgRot = new Angle(sumRot / count);

        _syncing = true;
        foreach (var gUid in net.Grids)
        {
            if (_gridCompQuery.TryComp(gUid, out var comp))
                ApplyAnchorToGrid(gUid, comp, avgPos, avgRot);
        }
        _syncing = false;
    }
}
