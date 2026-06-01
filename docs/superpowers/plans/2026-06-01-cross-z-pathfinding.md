# Cross-Z Pathfinding for NPCs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let NPCs pathfind and walk across Z-levels through ramps using a single A* query, replacing the GOAP move action's teleport crutch.

**Architecture:** Persistent pathfinding portals are created at each climbable ramp, connecting the walkable floor on the lower map to the walkable floor on the map above — turning the whole Z-network into one connected A* graph. The NPC steering follower is taught to keep walking onto the ramp at the cross-map seam instead of failing; the existing vertical physics (`CESharedZLevelsSystem`) then reparents the entity between levels exactly as it does for players. The GOAP move action becomes map-agnostic.

**Tech Stack:** C# (.NET), RobustToolbox ECS, Space Station 14 content fork (CrystallEdge). Tests: NUnit via `Content.IntegrationTests` `GameTest` harness.

**Design spec:** `docs/superpowers/specs/2026-06-01-cross-z-pathfinding-design.md`

**Conventions for every task:**
- CE code lives in `_CE/` folders, class names prefixed `CE`. Edits to upstream (non-`_CE`) files are wrapped in `// CrystallEdge: <reason>` … `// CrystallEdge end` comments.
- 4-space indent, file-scoped namespaces, Allman braces, `_camelCase` private fields, final newline.
- Every commit message ends with a trailer line: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Build is slow (~5 min cold). Build as a background process per `CLAUDE.md`. Incremental CE-only builds ~15–30s.
- Test harness conventions (from `Content.IntegrationTests/Tests/_CE/MeleeWeapon/CERatAttackTest.cs`): `GameTest` base exposes `Pair`, `Server`, `SEntMan`; resolve systems with `SEntMan.System<T>()`, managers with `Server.ResolveDependency<T>()`; run with `RunTicksSync(n)` / `RunSeconds(n)`; mutate state inside `Server.WaitPost(...)`; assert inside `Server.WaitAssertion(...)`; spawn with `await SpawnAtPosition("Proto", coords)`. Set floor tiles with `new Tile(tileMan["Plating"].TileId)`.

---

## File Structure

**Create:**
- `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalComponent.cs` — per-grid record of the ramp→portal handles + ramp facing, for cleanup and seam queries.
- `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalSystem.cs` — creates/removes portals at ramps; exposes the cross-map seam steering direction.
- `Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs` — integration tests.
- `Resources/Prototypes/_CE/Entities/Test/cross_z_pathfinding_test.yml` — a concrete (spawnable) test ramp. Only needed because every existing ramp prototype (`CEZLevelLadderBase` and friends) is `abstract: true`. If a concrete stairs prototype already exists in content, use its ID in the tests and skip this file.

**Modify (upstream, wrapped):**
- `Content.Server/NPC/Pathfinding/PathfindingSystem.cs` (~line 195) — allow cross-map portals.
- `Content.Server/NPC/Systems/NPCSteeringSystem.cs` — add CE dependency field.
- `Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs` (~lines 167-175 and 260-268) — cross-map seam.

**Modify (CE):**
- `Content.Server/_CE/GOAP/Actions/CEGOAPMoveToTargetActionSystem.cs` — remove all Z logic.

**Delete:**
- `Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheSystem.cs`
- `Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheComponent.cs`

---

## Task 1: Allow cross-map pathfinding portals

The pathfinder's `TryCreatePortal` rejects any two coordinates on different maps. Portals are the only A* mechanism that joins two grid graphs, so this rejection is the hard block on cross-Z paths. Relax it; docking is unaffected because docking only ever passes same-map coordinates.

This task's test is deterministic — two ordinary test grids plus a directly-created portal — so it does not depend on Z-network or mob behavior.

**Files:**
- Test: `Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs`
- Modify: `Content.Server/NPC/Pathfinding/PathfindingSystem.cs`

- [ ] **Step 1: Write the failing test**

Create `Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.GOAP.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._CE.Navigation;

[TestFixture]
public sealed class CECrossZPathfindingTest : GameTest
{
    // A portal between two grids on DIFFERENT maps must connect their navmeshes,
    // so an A* query that starts on one map can finish on the other.
    [Test]
    public async Task PortalConnectsGridsOnDifferentMaps()
    {
        var lower = await Pair.CreateTestMap();
        var upper = await Pair.CreateTestMap();

        var mapSys = SEntMan.System<SharedMapSystem>();
        var pathfinding = SEntMan.System<PathfindingSystem>();

        var lowerGrid = lower.Grid.Owner;
        var upperGrid = upper.Grid.Owner;

        await Server.WaitPost(() =>
        {
            var tileMan = Server.ResolveDependency<ITileDefinitionManager>();
            var tile = new Tile(tileMan["Plating"].TileId);

            // 1x3 strip of floor on each grid so there are walkable polys to path through.
            for (var x = 0; x < 3; x++)
            {
                mapSys.SetTile(lowerGrid, lower.Grid.Comp, new Vector2i(x, 0), tile);
                mapSys.SetTile(upperGrid, upper.Grid.Comp, new Vector2i(x, 0), tile);
            }
        });

        // Let the navmesh chunks rebuild (PathfindingSystem.UpdateGrid has a 0.45s cooldown).
        await RunTicksSync(60);

        var lowerStart = new EntityCoordinates(lowerGrid, new Vector2(0.5f, 0.5f));
        var lowerPortal = new EntityCoordinates(lowerGrid, new Vector2(2.5f, 0.5f));
        var upperPortal = new EntityCoordinates(upperGrid, new Vector2(0.5f, 0.5f));
        var upperEnd = new EntityCoordinates(upperGrid, new Vector2(2.5f, 0.5f));

        var created = false;
        await Server.WaitPost(() =>
        {
            created = pathfinding.TryCreatePortal(lowerPortal, upperPortal, out _);
        });

        Assert.That(created, Is.True, "TryCreatePortal must succeed across maps after the guard is relaxed.");

        await RunTicksSync(60);

        // Kick the async path request, then tick so PathfindingSystem.Update resolves it.
        // Do NOT block on the Task inside WaitPost — the resolving tick can't run while WaitPost blocks.
        Task<PathResultEvent>? pathTask = null;
        await Server.WaitPost(() =>
        {
            pathTask = pathfinding.GetPath(lowerStart, upperEnd, 0.5f, 0, 0, CancellationToken.None);
        });

        await RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(pathTask!.IsCompletedSuccessfully, Is.True, "Path request should have resolved.");
            var result = pathTask.Result;
            Assert.That(result.Result, Is.EqualTo(PathResult.Path),
                "A* should find a path spanning both maps via the portal.");
            var graphUids = result.Path.Select(p => p.GraphUid).Distinct().ToList();
            Assert.That(graphUids, Does.Contain(lowerGrid));
            Assert.That(graphUids, Does.Contain(upperGrid));
        });
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~CECrossZPathfindingTest.PortalConnectsGridsOnDifferentMaps"`
Expected: FAIL — `TryCreatePortal` returns `false` across maps, so `Assert.That(created, Is.True)` fails.

- [ ] **Step 3: Relax the cross-map guard**

In `Content.Server/NPC/Pathfinding/PathfindingSystem.cs`, find in `TryCreatePortal` (~line 195):

```csharp
            if (mapUidA != mapUidB || mapUidA == null)
            {
                return false;
            }
```

Replace with:

```csharp
            // CrystallEdge: allow portals between stacked Z-level maps (was: reject if mapUidA != mapUidB).
            // Docking is unaffected — it only ever creates same-map portals.
            if (mapUidA == null || mapUidB == null)
            {
                return false;
            }
            // CrystallEdge end
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~CECrossZPathfindingTest.PortalConnectsGridsOnDifferentMaps"`
Expected: PASS. If `NoPath`, raise the `RunTicksSync` counts (navmesh not built yet).

- [ ] **Step 5: Commit**

```bash
git add Content.Server/NPC/Pathfinding/PathfindingSystem.cs Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs
git commit -m "feat(nav): allow cross-map pathfinding portals for Z-levels

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `CEZPortalSystem` — portals at ramps + seam direction

A climbable ramp (`CEZLevelHighGroundComponent` whose `HeightCurve` rises from a low value up to ≥1) on map N should permanently connect, in the A* graph, the walkable floor on its low side to the walkable floor directly above it on map N+1. This system maintains those portals over the ramp's lifecycle and answers "which way do I walk to cross this seam?" for the steering follower.

Facing/geometry (verified against `CEZLevelLadderBase`, `HeightCurve: [1.05, 1.05, 0.1]`, and the prior action's usage):
- `rampFacing = Transform(ramp).LocalRotation.GetCardinalDir()` is the **downhill** direction.
- `uphillDir = rampFacing.GetOpposite()`.
- The ramp tile itself carries a hard top-wall fixture, so portal endpoints use the clear neighbour floor, not the ramp tile:
  - lower endpoint (map N): `lowApproach = rampTile + rampFacing.ToIntVec()` (floor on the low side).
  - upper endpoint (map N+1): `landing = rampTile` (same indices, on `MapAbove`) — where the entity arrives after ascending.

This relies on the CE invariant that each Z-level map entity is also a grid (it carries `MapGridComponent`), as used in `CESharedZLevelsSystem.Movement.cs`.

**Files:**
- Create: `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalComponent.cs`
- Create: `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalSystem.cs`
- Test: `Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs`

- [ ] **Step 1: Create the component**

Create `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalComponent.cs`:

```csharp
namespace Content.Server._CE.ZLevels.Pathfinding;

/// <summary>
/// Tracks the cross-Z pathfinding portals created for ramps anchored to this grid.
/// Lives on the grid that owns the ramps (the lower map of each portal pair).
/// </summary>
[RegisterComponent]
public sealed partial class CEZPortalComponent : Component
{
    /// <summary>
    /// Per-ramp portal data, keyed by the ramp entity.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, CEZRampPortal> Ramps = new();
}

/// <summary>
/// A single ramp's portal: its pathfinder handle plus the geometry the steering seam needs.
/// </summary>
public struct CEZRampPortal
{
    /// <summary>Handle returned by PathfindingSystem.TryCreatePortal, used to remove it.</summary>
    public int Handle;

    /// <summary>Tile the ramp occupies on this (lower) grid.</summary>
    public Vector2i RampTile;

    /// <summary>The direction walked to ascend the ramp (opposite of the ramp's facing).</summary>
    public Direction UphillDir;
}
```

- [ ] **Step 2: Implement `CEZPortalSystem`**

Create `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalSystem.cs`:

```csharp
using System.Linq;
using System.Numerics;
using Content.Server.NPC.Pathfinding;
using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Pathfinding;

/// <summary>
/// Maintains persistent cross-Z pathfinding portals at climbable ramps, turning a Z-network
/// into one connected A* graph, and answers the steering follower's "which way across the seam?"
/// query. Replaces the retired ladders cache.
/// </summary>
public sealed class CEZPortalSystem : EntitySystem
{
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private readonly EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;
    [Dependency] private readonly EntityQuery<CEZPortalComponent> _portalQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelHighGroundComponent, MapInitEvent>(OnRampInit);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, ComponentShutdown>(OnRampShutdown);
        SubscribeLocalEvent<CEZLevelHighGroundComponent, AnchorStateChangedEvent>(OnRampAnchorChanged);
        SubscribeLocalEvent<CEZLevelMapComponent, CEMapAddedIntoZNetworkEvent>(OnMapAddedToNetwork);
    }

    private void OnRampInit(Entity<CEZLevelHighGroundComponent> ent, ref MapInitEvent args)
    {
        TryCreateRampPortal(ent);
    }

    private void OnRampShutdown(Entity<CEZLevelHighGroundComponent> ent, ref ComponentShutdown args)
    {
        RemoveRampPortal(ent);
    }

    private void OnRampAnchorChanged(Entity<CEZLevelHighGroundComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            TryCreateRampPortal(ent);
        else
            RemoveRampPortal(ent);
    }

    // A ramp may be anchored before the map above joins the network; retry when the network grows.
    private void OnMapAddedToNetwork(Entity<CEZLevelMapComponent> ent, ref CEMapAddedIntoZNetworkEvent args)
    {
        var query = EntityQueryEnumerator<CEZLevelHighGroundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ramp, out var xform))
        {
            if (xform.MapUid != null && _zMapQuery.HasComponent(xform.MapUid.Value))
                TryCreateRampPortal((uid, ramp));
        }
    }

    /// <summary>
    /// True if this high ground is a ramp that can be climbed between floors: its curve dips low
    /// enough to step onto from the current floor and rises to (or past) the level boundary.
    /// Excludes flat ledges such as the default [1.05, 1.05] wall-top.
    /// </summary>
    private static bool IsClimbableRamp(CEZLevelHighGroundComponent comp)
    {
        return comp.HeightCurve.Count >= 2 && comp.HeightCurve.Min() <= 0.9f && comp.HeightCurve.Max() >= 1f;
    }

    private void TryCreateRampPortal(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (!IsClimbableRamp(ent.Comp))
            return;

        if (!_xformQuery.TryGetComponent(ent, out var xform) || !xform.Anchored)
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_gridQuery.TryGetComponent(gridUid.Value, out var grid))
            return;

        // Resolve the map above this ramp's map within its Z-network.
        var mapUid = xform.MapUid;
        if (mapUid == null || !_zMapQuery.TryGetComponent(mapUid.Value, out var zMap) || zMap.MapAbove is not { } aboveMap)
            return;

        if (!_gridQuery.TryGetComponent(aboveMap, out var aboveGrid))
            return;

        // Don't create twice.
        var portalComp = EnsureComp<CEZPortalComponent>(gridUid.Value);
        if (portalComp.Ramps.ContainsKey(ent.Owner))
            return;

        var rampWorld = _transform.GetWorldPosition(xform);
        var rampTile = _map.WorldToTile(gridUid.Value, grid, rampWorld);
        var downhillDir = xform.LocalRotation.GetCardinalDir(); // ramp faces downhill
        var uphillDir = downhillDir.GetOpposite();

        // Lower endpoint: the clear floor on the low (downhill) side of the ramp.
        var lowApproachTile = rampTile + downhillDir.ToIntVec();
        var lowApproach = _map.GridTileToLocal(gridUid.Value, grid, lowApproachTile);

        // Upper endpoint: the tile directly above the ramp (where the climber lands), on the map above.
        var landing = _map.GridTileToLocal(aboveMap, aboveGrid, rampTile);

        if (!_pathfinding.TryCreatePortal(lowApproach, landing, out var handle))
            return;

        portalComp.Ramps[ent.Owner] = new CEZRampPortal
        {
            Handle = handle,
            RampTile = rampTile,
            UphillDir = uphillDir,
        };
    }

    private void RemoveRampPortal(Entity<CEZLevelHighGroundComponent> ent)
    {
        if (!_xformQuery.TryGetComponent(ent, out var xform))
            return;

        var gridUid = xform.GridUid;
        if (gridUid == null || !_portalQuery.TryGetComponent(gridUid.Value, out var portalComp))
            return;

        if (!portalComp.Ramps.Remove(ent.Owner, out var ramp))
            return;

        _pathfinding.RemovePortal(ramp.Handle);
    }

    /// <summary>
    /// At a cross-map path seam, returns the world-space direction to keep walking so the existing
    /// vertical physics carries the NPC onto the ramp and across the level boundary.
    /// Ascend (target on the map above): walk uphill. Descend (target on the map below): walk downhill.
    /// </summary>
    [PublicAPI]
    public bool TryGetZSeamDirection(EntityUid npc, EntityCoordinates targetNode, out Vector2 worldDir)
    {
        worldDir = Vector2.Zero;

        if (!_xformQuery.TryGetComponent(npc, out var xform) || xform.MapUid is not { } ourMap)
            return false;

        if (!_zMapQuery.TryGetComponent(ourMap, out var ourZMap))
            return false;

        var targetMap = _transform.GetMap(targetNode);
        if (targetMap == null)
            return false;

        var ourWorld = _transform.GetWorldPosition(xform);

        // Ascend: ramp is on OUR grid; walk uphill.
        if (targetMap == ourZMap.MapAbove)
        {
            if (!TryGetNearestRamp(ourMap, ourWorld, out var ramp))
                return false;
            worldDir = ramp.UphillDir.ToVec();
            return true;
        }

        // Descend: ramp is on the grid BELOW us; walk downhill (toward the ramp).
        if (ourZMap.MapBelow is { } belowMap && targetMap == belowMap)
        {
            if (!TryGetNearestRamp(belowMap, ourWorld, out var ramp))
                return false;
            worldDir = ramp.UphillDir.GetOpposite().ToVec();
            return true;
        }

        return false;
    }

    private bool TryGetNearestRamp(EntityUid gridUid, Vector2 worldPos, out CEZRampPortal ramp)
    {
        ramp = default;

        if (!_portalQuery.TryGetComponent(gridUid, out var portalComp) ||
            !_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var originTile = _map.WorldToTile(gridUid, grid, worldPos);
        var bestDistSq = float.MaxValue;
        var found = false;

        foreach (var candidate in portalComp.Ramps.Values)
        {
            var dx = candidate.RampTile.X - originTile.X;
            var dy = candidate.RampTile.Y - originTile.Y;
            var distSq = dx * dx + dy * dy;

            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            ramp = candidate;
            found = true;
        }

        return found;
    }
}
```

- [ ] **Step 3: Write the failing test (auto-portal across a Z-network)**

This test needs a real two-level Z-network of map-grids. Add a shared helper and a test to `CECrossZPathfindingTest.cs`.

> **Map-grid setup:** each Z-level map entity must also be a grid (carry `MapGridComponent`) and the two must be wired with `CEZLevelsSystem` so `CEZLevelMapComponent.MapAbove` is set. The helper below creates a map, attaches a grid component to the map entity, and registers the pair as a Z-network. If `EnsureComponent<MapGridComponent>` does not yield a valid grid in this harness, build the map-grids the same way the game's Z mapping commands do (see `Content.Server/_CE/ZLevels/Mapping/Commands/CEAddMapAboveZNetworkCommand.cs` and `CEZLevelsSystem`) and keep the rest of the helper. The assertions do not change.

```csharp
    // (lowerMap, upperMap) — two map-grids wired into one Z-network (lower depth 0, upper depth 1),
    // each with a 6x1 floor strip.
    private async Task<(EntityUid lowerMap, EntityUid upperMap)> BuildTwoLevelZNetwork()
    {
        var mapSys = SEntMan.System<SharedMapSystem>();
        var zLevels = SEntMan.System<CEZLevelsSystem>();

        EntityUid lowerMap = default;
        EntityUid upperMap = default;

        await Server.WaitPost(() =>
        {
            var tileMan = Server.ResolveDependency<ITileDefinitionManager>();
            var tile = new Tile(tileMan["Plating"].TileId);

            mapSys.CreateMap(out var lowerId);
            mapSys.CreateMap(out var upperId);
            lowerMap = mapSys.GetMap(lowerId);
            upperMap = mapSys.GetMap(upperId);

            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(lowerMap);
            var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upperMap);

            for (var x = 0; x < 6; x++)
            {
                mapSys.SetTile(lowerMap, lowerGrid, new Vector2i(x, 0), tile);
                mapSys.SetTile(upperMap, upperGrid, new Vector2i(x, 0), tile);
            }

            var network = zLevels.CreateZNetwork();
            zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
            {
                [lowerMap] = 0,
                [upperMap] = 1,
            });
        });

        return (lowerMap, upperMap);
    }

    // Anchoring a ramp on the lower level must auto-create a portal, letting A* path up.
    [Test]
    public async Task RampCreatesPortalAcrossZNetwork()
    {
        var pathfinding = SEntMan.System<PathfindingSystem>();
        var xformSys = SEntMan.System<SharedTransformSystem>();

        var (lowerMap, upperMap) = await BuildTwoLevelZNetwork();

        await Server.WaitPost(() =>
        {
            // Ramp at tile (2,0) on the lower level, facing East (downhill = East).
            var ramp = SEntMan.SpawnEntity("CEZLevelTestRamp",
                new EntityCoordinates(lowerMap, new Vector2(2.5f, 0.5f)));
            xformSys.SetWorldRotation(SEntMan.GetComponent<TransformComponent>(ramp), Direction.East.ToAngle());
        });

        await RunTicksSync(90);

        // lowApproach = rampTile + downhill(East) = (3,0); landing on upper = rampTile = (2,0).
        var start = new EntityCoordinates(lowerMap, new Vector2(4.5f, 0.5f));
        var end = new EntityCoordinates(upperMap, new Vector2(0.5f, 0.5f));

        Task<PathResultEvent>? pathTask = null;
        await Server.WaitPost(() =>
        {
            pathTask = pathfinding.GetPath(start, end, 0.5f, 0, 0, CancellationToken.None);
        });
        await RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(pathTask!.IsCompletedSuccessfully, Is.True);
            var result = pathTask.Result;
            Assert.That(result.Result, Is.EqualTo(PathResult.Path),
                "Ramp portal should let A* path from the lower level to the upper level.");
            var graphUids = result.Path.Select(p => p.GraphUid).Distinct().ToList();
            Assert.That(graphUids, Does.Contain(lowerMap));
            Assert.That(graphUids, Does.Contain(upperMap));
        });
    }
```

The test ramp prototype `CEZLevelTestRamp` is created in Task 6 Step 1; create it now if running tasks in order would otherwise fail to spawn it.

- [ ] **Step 4: Run the test**

Run: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~RampCreatesPortalAcrossZNetwork"`
Expected: PASS. If `NoPath`: confirm the navmesh built (raise tick counts), the Z-network wired `MapAbove` (the helper), and that `lowApproach`/`landing` offsets match the ramp facing — if the portal only forms with the ramp facing the other way, flip the `downhillDir`/`uphillDir` derivation and re-run.

- [ ] **Step 5: Commit**

```bash
git add Content.Server/_CE/ZLevels/Pathfinding/ Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs
git commit -m "feat(zlevels): create pathfinding portals at ramps (CEZPortalSystem)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Teach the steering follower to cross the seam

When A* hands the follower a node on another map (the upper/lower endpoint of a ramp portal), `NPCSteeringSystem` currently fails with `NoPath`. Instead, ask `CEZPortalSystem` for the ramp direction and keep steering that way; the vertical physics then reparents the NPC, after which the node is on the same map and normal seeking resumes.

**Files:**
- Modify: `Content.Server/NPC/Systems/NPCSteeringSystem.cs`
- Modify: `Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs`

- [ ] **Step 1: Add the CE dependency**

In `Content.Server/NPC/Systems/NPCSteeringSystem.cs`, in the `[Dependency]` block (after the existing dependencies, ~line 70), add:

```csharp
    // CrystallEdge: cross-Z seam steering.
    [Dependency] private readonly Content.Server._CE.ZLevels.Pathfinding.CEZPortalSystem _ceZPortal = default!;
    // CrystallEdge end
```

- [ ] **Step 2: Replace the first seam bail-out**

In `Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs`, find (~line 167):

```csharp
        // Check if mapids match.
        var targetMap = _transform.ToMapCoordinates(targetCoordinates);
        var ourMap = _transform.ToMapCoordinates(ourCoordinates);

        if (targetMap.MapId != ourMap.MapId)
        {
            steering.Status = SteeringStatus.NoPath;
            return false;
        }

        var direction = targetMap.Position - ourMap.Position;
```

Replace with:

```csharp
        // Check if mapids match.
        var targetMap = _transform.ToMapCoordinates(targetCoordinates);
        var ourMap = _transform.ToMapCoordinates(ourCoordinates);

        Vector2 direction;

        if (targetMap.MapId != ourMap.MapId)
        {
            // CrystallEdge: cross-Z seam — the next node is on the map above/below at a ramp.
            // Keep walking onto the ramp; vertical physics reparents us, then this node is same-map.
            if (_ceZPortal.TryGetZSeamDirection(uid, targetCoordinates, out var seamDir))
            {
                direction = seamDir;
            }
            else
            {
                steering.Status = SteeringStatus.NoPath;
                return false;
            }
            // CrystallEdge end
        }
        else
        {
            direction = targetMap.Position - ourMap.Position;
        }
```

- [ ] **Step 3: Replace the second seam bail-out (after node dequeue)**

In the same file, find (~line 260):

```csharp
                targetMap = _transform.ToMapCoordinates(targetCoordinates);

                // Can't make it again.
                if (ourMap.MapId != targetMap.MapId)
                {
                    SetDirection(uid, mover, steering, Vector2.Zero);
                    steering.Status = SteeringStatus.NoPath;
                    return false;
                }

                // Gonna resume now business as usual
                direction = targetMap.Position - ourMap.Position;
                ResetStuck(steering, ourCoordinates);
```

Replace with:

```csharp
                targetMap = _transform.ToMapCoordinates(targetCoordinates);

                // CrystallEdge: cross-Z seam after dequeue — same handling as above.
                if (ourMap.MapId != targetMap.MapId)
                {
                    if (_ceZPortal.TryGetZSeamDirection(uid, targetCoordinates, out var seamDir))
                    {
                        direction = seamDir;
                    }
                    else
                    {
                        SetDirection(uid, mover, steering, Vector2.Zero);
                        steering.Status = SteeringStatus.NoPath;
                        return false;
                    }
                }
                else
                {
                    direction = targetMap.Position - ourMap.Position;
                }
                // CrystallEdge end
                ResetStuck(steering, ourCoordinates);
```

- [ ] **Step 4: Verify it compiles**

`direction` is now declared once as `Vector2 direction;` at the first site and only assigned at the second. Build the server (background) and confirm zero `error CS` (per `CLAUDE.md` build-check snippet).

- [ ] **Step 5: Commit**

```bash
git add Content.Server/NPC/Systems/NPCSteeringSystem.cs Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs
git commit -m "feat(nav): follow cross-Z path seams via ramp instead of failing

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Make the GOAP move action map-agnostic

Strip all Z-level logic from the action. With portals + the seam, the pathfinder and vertical physics handle floor changes; the action only registers steering to the target and reports status.

**Files:**
- Modify: `Content.Server/_CE/GOAP/Actions/CEGOAPMoveToTargetActionSystem.cs`

- [ ] **Step 1: Replace the whole system class**

Replace the entire contents of `Content.Server/_CE/GOAP/Actions/CEGOAPMoveToTargetActionSystem.cs` with:

```csharp
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Moves the NPC towards its current target entity.
/// Pathfinding (including across Z-levels via ramp portals) and the vertical physics that crosses
/// levels are handled by the steering/pathfinding layers; this action is map-agnostic.
/// </summary>
public sealed partial class CEGOAPMoveToTargetAction : CEGOAPActionBase<CEGOAPMoveToTargetAction>
{
    /// <summary>
    /// How close the NPC needs to get to the target to consider the action complete.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>
    /// How far the target must move before re-registering the steering destination.
    /// Prevents constant pathfinding recalculation while still tracking moving targets.
    /// </summary>
    [DataField]
    public float ReregisterThreshold = 1f;
}

public sealed partial class CEGOAPMoveToTargetActionSystem : CEGOAPActionSystem<CEGOAPMoveToTargetAction>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery = default!;

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMoveToTargetAction> args)
    {
        RegisterSteering(ent, args.Action);
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMoveToTargetAction> args)
    {
        if (!TryResolveCoords(ent, args.Action.Selector, out var coords))
            return;

        if (!_xformQuery.TryGetComponent(ent, out var npcXform))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        var sameMapAsTarget = npcXform.MapUid == _transform.GetMap(coords);

        if (_steeringQuery.TryComp(ent, out var steering))
        {
            // Re-register if the target moved significantly.
            if (steering.Coordinates.TryDistance(EntityManager, coords, out var delta) &&
                delta > args.Action.ReregisterThreshold)
            {
                var comp = _steering.Register(ent, coords);
                comp.Range = args.Action.Range;
            }

            switch (steering.Status)
            {
                case SteeringStatus.InRange:
                    // Only finished once we're actually on the target's map.
                    if (sameMapAsTarget)
                    {
                        args.Status = CEGOAPActionStatus.Finished;
                        return;
                    }

                    // In range of a path node but not yet on the target map: keep going.
                    RegisterSteering(ent, args.Action);
                    break;
                case SteeringStatus.NoPath:
                    args.Status = CEGOAPActionStatus.Failed;
                    return;
            }
        }

        args.Status = CEGOAPActionStatus.Running;
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPMoveToTargetAction> args)
    {
        _steering.Unregister(ent);
    }

    private void RegisterSteering(Entity<CEGOAPComponent> ent, CEGOAPMoveToTargetAction action)
    {
        if (!TryResolveCoords(ent, action.Selector, out var coords))
            return;

        if (!_xformQuery.TryGetComponent(ent, out _))
            return;

        var comp = _steering.Register(ent, coords);
        comp.Range = action.Range;
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Build the server (background). Expected: zero `error CS`. (The action no longer references `CEZLevelsLaddersCacheSystem`, `CESharedZLevelsSystem`, ladder/Z types, or `MapComponent`/`MapGridComponent` queries.)

- [ ] **Step 3: Commit**

```bash
git add Content.Server/_CE/GOAP/Actions/CEGOAPMoveToTargetActionSystem.cs
git commit -m "refactor(goap): make MoveToTarget map-agnostic, drop teleport

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Delete the ladders cache

It is now referenced by nothing (its only consumer, the GOAP action, was rewritten in Task 4).

**Files:**
- Delete: `Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheSystem.cs`
- Delete: `Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheComponent.cs`

- [ ] **Step 1: Confirm there are no remaining references**

Search the solution for `CEZLevelsLaddersCache` and `GetNearestLadder`. Expected: zero matches outside the two files being deleted.

- [ ] **Step 2: Delete the files**

```bash
git rm Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheSystem.cs Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheComponent.cs
```

- [ ] **Step 3: Build to verify it compiles**

Build the server (background). Expected: zero `error CS`.

- [ ] **Step 4: Commit**

```bash
git commit -m "chore(zlevels): remove ladders cache superseded by portals

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Behavioral test — a hostile mob chases prey up a Z-level

Prove the full loop with real mobs (no custom GOAP setup): a hostile GOAP mob (`CEMobRat`) on the lower level, aggroed onto a `CEMobHuman` on the upper level, walks the ramp and ends up on the human's map. This mirrors the existing `CERatAttackTest` aggro pattern (spawn a `CEAlarmInRange5` to wake/alert the mob) and adds the Z dimension. Test and the test ramp prototype live under `_CE`.

**Files:**
- Create: `Resources/Prototypes/_CE/Entities/Test/cross_z_pathfinding_test.yml`
- Test: `Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs`

- [ ] **Step 1: Create the concrete test ramp prototype**

Every ramp prototype in content is `abstract` (`CEZLevelLadderBase`, …), so define one concrete, spawnable ramp for the tests. Create `Resources/Prototypes/_CE/Entities/Test/cross_z_pathfinding_test.yml`:

```yaml
# Concrete, spawnable ramp for integration tests. Faces East (downhill = East), rises 0.1 -> 1.05.
- type: entity
  parent: CEZLevelLadderBase
  id: CEZLevelTestRamp
  name: test stairs
  suffix: TEST
```

If the YAML linter rejects this because the inherited `Sprite` has no `sprite:` path, copy the concrete `- type: Sprite` block (with a real `sprite:` rsi path) from an existing in-map stairs entity into this prototype. If a non-abstract stairs prototype already exists in content, delete this file and use that prototype's ID in the test instead.

- [ ] **Step 2: Write the failing test**

Append to `CECrossZPathfindingTest.cs` (reuses `BuildTwoLevelZNetwork` from Task 2):

```csharp
    // A hostile mob on the lower level, aggroed onto prey on the upper level,
    // must walk the ramp and end up on the prey's map (no teleport).
    [Test]
    public async Task HostileMobWalksRampToReachPreyAbove()
    {
        var xformSys = SEntMan.System<SharedTransformSystem>();

        var (lowerMap, upperMap) = await BuildTwoLevelZNetwork();

        EntityUid hunter = default;

        await Server.WaitPost(() =>
        {
            var ramp = SEntMan.SpawnEntity("CEZLevelTestRamp",
                new EntityCoordinates(lowerMap, new Vector2(3.5f, 0.5f)));
            xformSys.SetWorldRotation(SEntMan.GetComponent<TransformComponent>(ramp), Direction.East.ToAngle());

            // Prey on the upper level; hunter on the lower level near the ramp's low side.
            SEntMan.SpawnEntity("CEMobHuman", new EntityCoordinates(upperMap, new Vector2(1.5f, 0.5f)));
            hunter = SEntMan.SpawnEntity("CEMobRat", new EntityCoordinates(lowerMap, new Vector2(5.5f, 0.5f)));
        });

        // Build navmesh / portal, let the mob initialize.
        await RunTicksSync(90);

        // Aggro the hunter exactly like CERatAttackTest does.
        await Server.WaitPost(() =>
        {
            SEntMan.SpawnEntity("CEAlarmInRange5", new EntityCoordinates(lowerMap, new Vector2(5.5f, 0.5f)));
        });

        // Give the GOAP agent time to plan and the NPC time to walk up the ramp.
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            var hunterXform = SEntMan.GetComponent<TransformComponent>(hunter);
            Assert.That(hunterXform.MapUid, Is.EqualTo(upperMap),
                "Hostile mob should have walked the ramp up to the prey's level.");
        });
    }
```

- [ ] **Step 3: Run the test**

Run: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~HostileMobWalksRampToReachPreyAbove"`
Expected: PASS once the feature works. If it fails, debug with `superpowers:systematic-debugging`:
- Confirm the mob aggroed at all (mirror `CERatAttackTest`'s checks: `CEActiveGOAPComponent` present, `CEGOAPKnowledgeCacheComponent.Enemies` non-empty). If it never aggros across a Z-level, move the prey closer in XY or place the alarm so both are within its range, and/or start them on the same level a tile apart — the point of this test is the *traversal*, so any reliable aggro is fine.
- Confirm `GetPath` from hunter to prey spans both maps (re-use Task 2's assertion). If `NoPath`, the portal/predicate/offsets are wrong.
- If the hunter reaches the ramp's low approach tile but stalls, the seam direction is wrong — verify `TryGetZSeamDirection` returns the uphill vector and that the mob's `CEZPhysicsComponent` is awake while moving.
- Increase `RunSeconds` if it simply needs more time.

- [ ] **Step 4: Commit**

```bash
git add Resources/Prototypes/_CE/Entities/Test/cross_z_pathfinding_test.yml Content.IntegrationTests/Tests/_CE/Navigation/CECrossZPathfindingTest.cs
git commit -m "test(zlevels): hostile mob chases prey up a ramp across Z-levels

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Full verification

- [ ] **Step 1: Run the whole new test fixture**

Run: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~CECrossZPathfindingTest"`
Expected: all three tests PASS.

- [ ] **Step 2: Run the YAML linter**

Run: `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj`
Expected: no errors from the new prototype file (`Ошибок: 0`).

- [ ] **Step 3: Manual in-game smoke check (optional but recommended)**

Load a dungeon Z-network that has a ramp and a GOAP mob, aggro it from a different floor, and confirm it paths up/down the ramp and never visibly teleports. Use `superpowers:verify` if helpful.

---

## Self-Review

**Spec coverage:**
- Cross-map portals (spec blocker #1) → Task 1.
- Steering seam (spec blocker #2) → Task 3.
- Portal creation at ramps + directionality + climbable predicate (spec §CEZPortalSystem, Δ directional ramps, risk #4) → Task 2.
- GOAP action map-agnostic (spec §D) → Task 4.
- Retire ladders cache (spec §E) → Task 5.
- Vertical physics does the transition (spec key enabler, risk #1 body-wake) → relied on, no code; confirmed wakes via `OnMoveEvent → DirtyMovement → RefreshBody`.
- Tests: deterministic path-spans (Tasks 1, 2) + behavioral ascend with real mobs (Task 6). Descent and approach-side are covered structurally by the same 2-way portal and seam logic; add a descent variant of Task 6 if a regression appears.

**Placeholder scan:** No `TODO`/`TBD` in shipped code. Deliberately-conditional setup is flagged with concrete fallbacks: the map-grid helper (use the game's Z-mapping path if `EnsureComponent<MapGridComponent>` is insufficient), the test ramp sprite (copy from a real stairs if the linter complains), and the aggro placement (move mobs closer if cross-Z aggro doesn't trigger). The offset-sign and tick-count adjustments are validated by the tests.

**Type consistency:** `CEZRampPortal` fields (`Handle`, `RampTile`, `UphillDir`) are used consistently across `CEZPortalComponent`, `TryCreateRampPortal`, `RemoveRampPortal`, `TryGetNearestRamp`, and `TryGetZSeamDirection`. `TryGetZSeamDirection(EntityUid, EntityCoordinates, out Vector2)` matches its call sites in Task 3. `BuildTwoLevelZNetwork` returns `(EntityUid lowerMap, EntityUid upperMap)` and is consumed identically in Tasks 2 and 6. Mob/aggro prototype IDs (`CEMobRat`, `CEMobHuman`, `CEAlarmInRange5`) and harness calls (`Pair`, `Server`, `SEntMan`, `SpawnAtPosition`, `RunTicksSync`, `RunSeconds`) match the existing `CERatAttackTest`.
