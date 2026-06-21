/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
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
/// Synchronizes all grids in a z-network as a single rigid body.
///
/// Data: each grid stores NetworkOffset (anchor-local XY) and NetworkRotation (relative to anchor).
/// All grids in a consistent network derive the same virtual anchor point.
///
/// At link time: position is snapped to tile grid; rotation to 90° if correction &lt; 45°.
/// Static anchors (planets) are never moved — their offset is recorded exactly.
/// After each physics substep, velocity consensus and position correction keep grids coherent.
/// </summary>
public sealed partial class CEZGridSyncSystem : VirtualController
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CEZLevelsSystem _zlevels = default!;
    [Dependency] private CEZGridConnectorSystem _connectorSystem = default!;

    [Dependency] private EntityQuery<CEZGridComponent> _gridCompQuery = default!;
    [Dependency] private EntityQuery<CEZGridNetworkComponent> _netQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapCompQuery = default!;

    private bool _inPhysicsTick;
    private bool _syncing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZGridComponent, CEGridLinkedEvent>(OnGridLinked);
        SubscribeLocalEvent<CEZGridComponent, CEGridUnlinkedEvent>(OnGridUnlinked);
        SubscribeLocalEvent<CEZGridComponent, MoveEvent>(OnGridMoved);
        SubscribeLocalEvent<CEZGridComponent, MassDataChangedEvent>(OnMassChanged);
    }

    private bool IsStaticAnchor(EntityUid gridUid)
    {
        return _mapCompQuery.HasComponent(gridUid)
               || (_physicsQuery.TryComp(gridUid, out var body) && !IsMoveable(body));
    }

    private static bool IsMoveable(PhysicsComponent body)
    {
        return (body.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) != 0x0;
    }

    private static Vector2 SnapToTile(Vector2 v, float tileSize)
    {
        return new Vector2(MathF.Round(v.X / tileSize) * tileSize,
                           MathF.Round(v.Y / tileSize) * tileSize);
    }

    private static Angle SnapToQuadrant(Angle a)
    {
        return Angle.FromDegrees(Math.Round(a.Degrees / 90.0) * 90.0);
    }

    // Derives the virtual network anchor from one grid's world transform + stored offsets.
    // Every grid in a consistent network yields the same anchor.
    private (Vector2 Pos, Angle Rot) GetAnchorFromGrid(EntityUid refGrid)
    {
        var refPos = _transform.GetWorldPosition(refGrid);
        var refRot = _transform.GetWorldRotation(refGrid);

        if (!_gridCompQuery.TryComp(refGrid, out var c))
            return (refPos, refRot);

        var rot = refRot - c.NetworkRotation;
        return (refPos - rot.RotateVec(c.NetworkOffset), rot);
    }

    private void ApplyAnchorToGrid(EntityUid gridUid, CEZGridComponent comp,
        Vector2 anchorPos, Angle anchorRot)
    {
        _transform.SetWorldPositionRotation(gridUid,
            anchorPos + anchorRot.RotateVec(comp.NetworkOffset),
            anchorRot + comp.NetworkRotation);
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

    private void OnGridLinked(Entity<CEZGridComponent> ent, ref CEGridLinkedEvent ev)
    {
        if (!_netQuery.TryComp(ev.Network, out var net))
            return;

        var worldPos = _transform.GetWorldPosition(ent);
        var worldRot = _transform.GetWorldRotation(ent);

        if (net.Grids.Count == 1)
        {
            ent.Comp.NetworkOffset = Vector2.Zero;
            ent.Comp.NetworkRotation = Angle.Zero;
        }
        else
        {
            // Pick anchor: static anchor takes priority, otherwise any existing grid
            var anchorGrid = net.Grids.FirstOrDefault(IsStaticAnchor);
            if (anchorGrid == default)
                anchorGrid = net.Grids.First(g => g != ent.Owner);
            var (anchorPos, anchorRot) = GetAnchorFromGrid(anchorGrid);

            var localOffset = new Angle(-anchorRot.Theta).RotateVec(worldPos - anchorPos);
            var relRot = worldRot - anchorRot;

            if (IsStaticAnchor(ent.Owner))
            {
                // Planet cannot be moved — record exact position so the invariant holds.
                ent.Comp.NetworkOffset = localOffset;
                ent.Comp.NetworkRotation = relRot;
            }
            else
            {
                var tileSize = _mapGridQuery.GetComponent(ent.Owner).TileSize;
                var snappedOffset = SnapToTile(localOffset, tileSize);
                var snappedRot = SnapToQuadrant(relRot);

                // Always snap when joining a static-anchor network.
                // For flying-only merges, only snap if the correction is small (≤ 1 tile, < 45°).
                var shouldSnap = net.HasStaticAnchor
                                 || ((snappedOffset - localOffset).Length() < tileSize
                                     && Math.Abs((snappedRot - relRot).Theta) < Math.PI * 0.25);
                if (shouldSnap)
                {
                    ent.Comp.NetworkOffset = snappedOffset;
                    ent.Comp.NetworkRotation = snappedRot;
                    _syncing = true;
                    _transform.SetWorldPositionRotation(ent.Owner,
                        anchorPos + anchorRot.RotateVec(snappedOffset),
                        anchorRot + snappedRot);
                    _syncing = false;
                    _connectorSystem.MarkDirty();
                }
                else
                {
                    ent.Comp.NetworkOffset = localOffset;
                    ent.Comp.NetworkRotation = relRot;
                }
            }
        }

        ent.Comp.CachedFixturesMass = _physicsQuery.TryComp(ent.Owner, out var body)
            ? body.FixturesMass : 0f;

        RecalculateNetworkCache((ev.Network, net));

        // When a static anchor is present: re-compute and snap all moveable grids
        // relative to it, then zero their velocities.
        // This also handles the case where the planet joined after the flying grids.
        if (net.HasStaticAnchor)
        {
            var staticGrid = net.Grids.FirstOrDefault(IsStaticAnchor);
            if (staticGrid != default)
            {
                var (sAnchorPos, sAnchorRot) = GetAnchorFromGrid(staticGrid);
                var sTileSize = _mapGridQuery.GetComponent(staticGrid).TileSize;

                _syncing = true;
                foreach (var gUid in net.Grids)
                {
                    if (IsStaticAnchor(gUid) || !_gridCompQuery.TryComp(gUid, out var gComp))
                        continue;

                    var gPos = _transform.GetWorldPosition(gUid);
                    var gRot = _transform.GetWorldRotation(gUid);
                    gComp.NetworkOffset = SnapToTile(
                        new Angle(-sAnchorRot.Theta).RotateVec(gPos - sAnchorPos), sTileSize);
                    gComp.NetworkRotation = SnapToQuadrant(gRot - sAnchorRot);
                    ApplyAnchorToGrid(gUid, gComp, sAnchorPos, sAnchorRot);

                    if (_physicsQuery.TryComp(gUid, out var gBody) && IsMoveable(gBody))
                    {
                        PhysicsSystem.SetLinearVelocity(gUid, Vector2.Zero, body: gBody);
                        PhysicsSystem.SetAngularVelocity(gUid, 0f, body: gBody);
                    }
                }
                _syncing = false;
                _connectorSystem.MarkDirty();
            }
        }
    }

    private void OnGridUnlinked(Entity<CEZGridComponent> ent, ref CEGridUnlinkedEvent ev)
    {
        ent.Comp.NetworkOffset = Vector2.Zero;
        ent.Comp.NetworkRotation = Angle.Zero;
        ent.Comp.CachedFixturesMass = 0f;

        if (_netQuery.TryComp(ev.Network, out var net))
            RecalculateNetworkCache((ev.Network, net));
    }

    private void OnMassChanged(Entity<CEZGridComponent> ent, ref MassDataChangedEvent args)
    {
        ent.Comp.CachedFixturesMass = _physicsQuery.TryComp(ent.Owner, out var body)
            ? body.FixturesMass : 0f;

        if (_zlevels.TryGetGridNetwork(ent.Owner, out var network))
            RecalculateNetworkCache(network);
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

        if (network.Comp.HasStaticAnchor)
        {
            var staticGrid = network.Comp.Grids.FirstOrDefault(IsStaticAnchor);
            if (staticGrid == default)
                return;
            var (anchorPos, anchorRot) = GetAnchorFromGrid(staticGrid);
            _syncing = true;
            ApplyAnchorToGrid(ent.Owner, ent.Comp, anchorPos, anchorRot);
            _syncing = false;
            return;
        }

        var (newAnchorPos, newAnchorRot) = GetAnchorFromGrid(ent.Owner);
        _syncing = true;
        foreach (var otherGrid in network.Comp.Grids)
        {
            if (otherGrid == ent.Owner || !_gridCompQuery.TryComp(otherGrid, out var otherComp))
                continue;
            ApplyAnchorToGrid(otherGrid, otherComp, newAnchorPos, newAnchorRot);
        }
        _syncing = false;
    }

    // Computes mass-weighted velocity consensus for a network (rigid body: v_i = vCom + ω×r_i).
    // Called twice per substep: before (to pre-sync) and after (to redistribute impulses).
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

        var P = Vector2.Zero;
        var L = 0f;
        var Itotal = 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_physicsQuery.TryComp(gUid, out var body) || !_gridCompQuery.TryComp(gUid, out var gComp))
                continue;

            var mass = gComp.CachedFixturesMass;
            var r = _transform.GetWorldPosition(gUid) - com;

            if (mass > 0f)
            {
                P += body.LinearVelocity * mass;
                L += mass * (r.X * body.LinearVelocity.Y - r.Y * body.LinearVelocity.X);
            }

            if (body.Inertia > 0f)
            {
                L += body.Inertia * body.AngularVelocity;
                Itotal += body.Inertia + mass * (r.X * r.X + r.Y * r.Y);
            }
        }

        var vCom = P / totalMass;
        var omega = Itotal > 0f ? L / Itotal : 0f;

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

        // Second consensus pass: redistributes any collision impulse applied to a single grid
        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count >= 2 && !net.HasStaticAnchor)
                RunRigidBodyConsensus(net);
        }

        // Average the anchor across all grids to eliminate position drift from the substep
        CorrectNetworkDrift();
    }

    // Each grid independently computes the anchor; floating-point and impulse asymmetry
    // cause them to disagree by a small amount per substep. We average and re-apply.
    private void CorrectNetworkDrift()
    {
        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count < 2 || net.HasStaticAnchor)
                continue;

            var sumPos = Vector2.Zero;
            var sumRot = 0.0;

            foreach (var gUid in net.Grids)
            {
                var (aPos, aRot) = GetAnchorFromGrid(gUid);
                sumPos += aPos;
                sumRot += aRot.Theta;
            }

            var count = net.Grids.Count;
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
}
