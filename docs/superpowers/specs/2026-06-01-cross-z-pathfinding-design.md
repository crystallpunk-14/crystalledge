# Cross-Z Pathfinding for NPCs — Design

- **Date:** 2026-06-01
- **Status:** Approved (pending implementation plan)
- **Area:** `Content.Server/_CE/GOAP`, `Content.Server/NPC/Pathfinding`, `Content.Server/NPC/Systems`, `Content.Shared/_CE/ZLevels`

## Problem

NPC navigation cannot pathfind across Z-levels. The pathfinder (`Content.Server/NPC/Pathfinding`)
and the steering follower (`Content.Server/NPC/Systems/NPCSteeringSystem`) both operate on a single
map: an A* path cannot span the stacked maps that make up a Z-network, and the follower refuses any
path node that resolves to a different map. An NPC therefore has no way to compute or follow a route
from one floor to another.

We want a single A* query to produce a path through stacked maps, routed through ramps, and the NPC
to follow it across floors. The actual level transition reuses the existing vertical physics that
already carries players up and down ramps; the navigation layer only has to walk the NPC onto the
correct ramp from the correct side.

## Goals

- A single A* query produces a path that spans multiple Z-level maps, routed through ramps.
- NPC follows that path; the Z transition happens by walking onto the ramp and letting the existing
  vertical physics reparent it (identical to player traversal) — no teleporting the entity.
- Cross-Z navigation works for **all** NPCs (no opt-in gate).
- Respect ramp directionality: a unit may only mount a ramp from its low side when ascending, and
  from its high side when descending (no entering "from the back").

## Non-Goals

- No engine (RobustToolbox) changes. (Confirmed unnecessary — see Blocker Analysis.)
- No one-way pathfinding portals (not implemented in content; not needed here).
- No change to how players cross Z-levels.
- No new gating `PathFlag` / per-NPC opt-in.
- Not solving incidental ramp crossing (an NPC walking over a ramp as normal floor auto-climbs) —
  that is pre-existing behavior shared with players and is mapper-controlled.

## Blocker Analysis (content vs engine)

**Verdict: zero engine blockers.** Every blocker is in `Content.Server` (editable). RobustToolbox
is untouched.

### Engine (RobustToolbox) — clear
- Cross-Z movement is a reparent to the stacked map at the same world XY (`SetMapCoordinates`),
  fully supported and already used by `CESharedZLevelsSystem.TryMove`
  (`Content.Shared/_CE/ZLevels/Core/EntitySystems/CESharedZLevelsSystem.Movement.cs:290`).
- `EntityCoordinates.TryDistance` returns false across maps, but A* has an `Equals(endNode)`
  fallback and the heuristic transforms across graphs via world matrices, so this is not a hard
  block.

### Content blockers
1. **Portal cross-map ban** — `Content.Server/NPC/Pathfinding/PathfindingSystem.cs:195`:
   `if (mapUidA != mapUidB || mapUidA == null) return false;`. Portals are the only mechanism that
   joins two grid graphs for A*. Currently same-map only (used solely by docking,
   `Content.Server/Shuttles/Systems/DockingSystem.cs`). This is the hard block.
2. **Steering bails on map mismatch** — `Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs:171`
   and `:263`: `if (targetMap.MapId != ourMap.MapId) { steering.Status = NoPath; return false; }`.
   The follower refuses any next node on another map.
3. **No cross-Z portals exist** — a missing piece, not a ban. Needs a CE system to create/maintain
   portals at ramp tiles between vertically-adjacent maps.

### Key enabler: vertical physics already crosses levels
Vertical physics in `Content.Shared/_CE/ZLevels/Core/EntitySystems/CESharedZLevelsSystem.Update.cs:134-154`:
walking onto a ramp (`CEZLevelHighGroundComponent` with a rising `HeightCurve`) raises the entity's
`LocalPosition` via `AutoStep`; at `>= 1` it auto-`TryMoveUp` (reparent up), at `< 0` it
auto-`TryMoveDown`. This is exactly how players cross levels. NPCs (base mob prototype has
`CEZPhysicsComponent`, `Resources/Prototypes/Entities/Mobs/base.yml`) get this for free — they only
need to be *walked onto the ramp* in the correct direction.

The heuristic `GetDiff` (`Content.Server/NPC/Pathfinding/PathfindingSystem.Distance.cs:31-40`)
already transforms positions between graphs via world matrices. Z-level maps are spatially stacked
and aligned (same world XY), so this works numerically across maps and stays admissible.

Each Z-level map entity **is also its own grid** (it carries `MapGridComponent` +
`GridPathfindingComponent`; see `_gridQuery.TryComp(xform.MapUid, ...)` in
`CESharedZLevelsSystem.Movement.cs:104`). So a portal endpoint on a level is simply
`EntityCoordinates(mapUid, worldXY)`.

## Decisions

- **Edit strategy:** surgical wrapped edits to upstream files are acceptable (per `CLAUDE.md`).
- **Scope:** cross-Z navigation is on for all NPCs; no gating `PathFlag`.
- **Cache:** `CEZLevelsLaddersCacheSystem` is used only by the GOAP action being refactored, so it
  is retired (deleted), not extended.
- **Directionality:** enforced by portal-endpoint geometry (low-approach ↔ landing), not by one-way
  edges.

## Architecture (Approach A: persistent ramp portals)

```
ramp anchored (CEZLevelHighGroundComponent, climbable HeightCurve)
   -> CEZPortalSystem creates a PathPortal:                       [NEW]
        lowApproach@mapN  <->  landing@mapAbove
   -> A* sees ONE connected graph across floors                   [free, via portal neighbors]
NPC query GetPathSafe(npc, target)  ->  multi-map poly path
   -> NPCSteeringSystem follows nodes on the current map
   -> at the vertical portal seam: steer uphill (ascend) / downhill (descend) across the ramp  [NEW]
   -> vertical physics raises/drops LocalPosition -> TryMoveUp/Down reparents                  [exists]
   -> ourMap now == next node's map -> normal seek resumes
GOAP MoveToTarget action: Register(target) + report status        [gutted of teleport]
```

### Component / system changes

#### A. `CEZPortalSystem` (new) — `Content.Server/_CE/ZLevels/Pathfinding/CEZPortalSystem.cs`
Owns the lifecycle of cross-Z pathfinding portals and the per-ramp metadata the steering seam needs
(replacing the deleted ladders cache).

- Subscribes ramp lifecycle on `CEZLevelHighGroundComponent`: `MapInitEvent`,
  `AnchorStateChangedEvent`, `ComponentShutdown`, plus `CEZLevelNetworkUpdatedEvent` (so a ramp whose
  `MapAbove` only exists after the network grows gets its portal created then).
- **Climbable-ramp predicate:** only ramps actually traversable between floors get a portal — the
  `HeightCurve` must rise from a low value (reachable from the current floor) up to `>= 1`. A flat
  ledge such as the default `[1.05, 1.05]` (a wall-top) is excluded. Exact thresholds verified
  against authored, player-traversable ramps during implementation.
- **Facing / direction:** `uphillDir = ramp.LocalRotation.GetCardinalDir()` (the direction walked to
  ascend), read directly from the ramp entity's transform (as the old cache did).
- **Portal endpoints (directionality by construction):**
  - low endpoint (map N): `lowApproach = rampTile + uphillDir.Opposite` — the downhill approach tile.
  - high endpoint (map N+1, `CEZLevelMapComponent.MapAbove`): the landing tile a player reaches after
    ascending (approximately `rampTile + uphillDir`; exact tile verified against player traversal).
  - `TryCreatePortal(EntityCoordinates(mapN, lowApproachWorldXY), EntityCoordinates(mapAbove, landingWorldXY))`,
    storing the returned `handle` keyed by the ramp.
  - The portal is 2-way (`AddNeighbors` is bidirectional), which is correct: ascend uses
    low→landing, descend uses landing→low, each from the geometrically valid side.
- On ramp removal / map removal: `RemovePortal(handle)`.
- A ramp on the top map (no `MapAbove`) gets no portal.
- Exposes a seam-direction query for the steering edit, e.g.
  `bool TryGetZSeamDirection(EntityUid npc, EntityCoordinates targetNode, out Vector2 worldDir)`:
  given the NPC's current map and whether the target node's map is the NPC's `MapAbove` or
  `MapBelow`, returns the uphill (ascend) or downhill (descend) world-space steer vector for the
  relevant ramp.

#### B. Upstream wrapped edit 1 — allow cross-map portals
`Content.Server/NPC/Pathfinding/PathfindingSystem.cs:195`:
```csharp
// CrystallEdge: allow portals between stacked Z-level maps (was: reject if mapUidA != mapUidB)
if (mapUidA == null || mapUidB == null)
    return false;
// CrystallEdge end
```
Docking is unaffected — it only ever passes same-map coordinates. The downstream
`GridPathfindingComponent` presence checks remain.

#### C. Upstream wrapped edit 2 — steering seam
`Content.Server/NPC/Systems/NPCSteeringSystem.Context.cs` at the two map-mismatch sites (`:171` and
`:263`). When the next node is on another map, instead of immediately failing, ask `CEZPortalSystem`
whether this is a stacked Z-seam and, if so, steer along the ramp:
```csharp
// CrystallEdge: vertical Z seam — don't bail; walk the ramp and let vertical physics reparent us.
if (targetMap.MapId != ourMap.MapId)
{
    if (_ceZNav.TryGetZSeamDirection(uid, targetCoordinates, out var rampDir))
    {
        direction = rampDir; // uphill (target on MapAbove) / downhill (target on MapBelow)
        // Fall through to ApplySeek. Once LocalPosition crosses, TryMoveUp/Down flips ourMap,
        // the node becomes same-map next tick, and normal seek resumes / dequeues it.
    }
    else
    {
        steering.Status = SteeringStatus.NoPath; // genuinely unreachable — unchanged behavior
        return false;
    }
}
// CrystallEdge end
```
Adds a CE `[Dependency]` on `CEZPortalSystem` to `NPCSteeringSystem` (wrapped). `direction` is the
existing world-space vector the method already feeds into `ApplySeek`.

#### D. GOAP action — `Content.Server/_CE/GOAP/Actions/CEGOAPMoveToTargetActionSystem.cs`
Reduced to map-agnostic steering: on startup, resolve the target coordinates and
`_steering.Register(ent, coords)` (set `Range`); on update, re-register if the target moved past
`ReregisterThreshold`; report `Finished` when steering is `InRange` and on the same map as the
target, `Failed` on `NoPath`, otherwise `Running`. All Z handling — ramp selection and the level
transition — is delegated to the pathfinder, the portal graph, and vertical physics; the action
performs no Z-level logic of its own.

#### E. Retire the ladders cache
Delete `Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheSystem.cs` and
`Content.Server/_CE/ZLevels/LaddersCache/CEZLevelsLaddersCacheComponent.cs`. They are referenced only
by the GOAP action being rewritten.

## Data flow walkthrough

**Ascend (NPC on map N, target on map N+1):**
1. A* returns `… → lowApproach@N → (portal) → landing@N+1 → … → target`.
2. Steering follows same-map nodes to `lowApproach@N` (normal seek).
3. Next node is `landing@N+1`; map mismatch triggers the seam. `CEZPortalSystem` returns the ramp's
   uphill vector. NPC walks from the downhill tile onto the ramp's low edge.
4. Vertical physics raises `LocalPosition` to `>= 1` → `TryMoveUp` reparents NPC to map N+1 at the
   same world XY (the landing).
5. `ourMap` is now N+1; `landing@N+1` is same-map, NPC is on it → arrive → dequeue → resume.

**Descend (NPC on map N+1, target on map N):** symmetric. A* routes
`… → landing@N+1 → (portal) → lowApproach@N → …`. The seam steers downhill (toward the ramp); the NPC
steps onto the ramp top from the high side; `LocalPosition < 0` → `TryMoveDown`; resume on map N.

Directionality holds by construction: the only graph edge into the upper level originates at the
downhill approach tile, so an ascending NPC always mounts from the low side; a descending NPC always
starts from the landing (high) side.

## Risks / verification (resolve during implementation)

1. **Body wake.** The NPC must be in `CESharedZLevelsSystem._activeBodies` so `ProcessZPhysics` /
   `AutoStep` runs while it walks the ramp. Player parity suggests yes (a `MoveEvent` →
   `DirtyMovement` wakes the body), but verify in
   `Content.Shared/_CE/ZLevels/Core/EntitySystems/CESharedZLevelsSystem.Movement.Dirty.cs`. If NPC
   movement does not wake the body, wake it explicitly at the seam.
2. **Seam climb direction.** Steering must drive the NPC *along* the ramp (uphill/downhill), not
   toward the stacked point (which is ~0 horizontal and would stall it at the base). Handled by the
   `CEZPortalSystem` direction query; this is the primary tuning risk.
3. **Endpoint tile selection / descent geometry.** `lowApproach` and `landing` offsets must match how
   players actually traverse the ramp (including the descent case, where the upper tile must be
   step-down-able). Verify against authored maps.
4. **Climbable-ramp predicate.** Not every `CEZLevelHighGroundComponent` is a climbable ramp — a flat
   high ground (e.g. the default `[1.05, 1.05]` wall-top) is a ledge, not an inter-floor ramp. The
   portal system must test for a rising `HeightCurve` so only genuinely traversable ramps get portals.
5. **Portal upkeep on chunk rebuild.** `PathfindingSystem.UpdateGrid` re-links portal neighbors each
   rebuild (already handled); confirm the chosen endpoint polys survive (non-empty tile).
6. **Deferred portal creation.** A ramp may be cached before its `MapAbove` joins the network; create
   the portal on `CEZLevelNetworkUpdatedEvent` once `MapAbove` exists.
7. **Performance.** One extra graph edge per ramp; A* `NodeLimit` and time-slicing already bound query
   cost. Expected negligible.

## Testing

Integration tests (`Content.IntegrationTests`):
- Build a 2-map Z-network with one ramp and its portal; place an NPC on the lower map and a target on
  the upper map. Assert `GetPathSafe` returns a path whose polys span both map UIDs.
- Drive the NPC: assert its `MapUid` changes to the upper map within N ticks, it reaches the target,
  and no teleport (`SetWorldPosition` / `SetMapCoordinates` from the GOAP action) is invoked.
- Symmetric descent test.
- Approach-side test: starting positions that would require mounting from the back/side must not
  produce a path that climbs from the wrong side.
- Unreachable target (different Z-network) → `NoPath` / `Failed`.
