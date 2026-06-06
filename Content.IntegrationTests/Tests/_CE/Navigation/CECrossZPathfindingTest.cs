using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.Pathfinding;
using Content.Server._CE.ZLevels.Core;
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
        Task<PathResultEvent> pathTask = null!;
        await Server.WaitPost(() =>
        {
            pathTask = pathfinding.GetPath(lowerStart, upperEnd, 0.5f, 0, 0, CancellationToken.None);
        });

        await RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(pathTask.IsCompletedSuccessfully, Is.True, "Path request should have resolved.");
            var result = pathTask.GetAwaiter().GetResult();
            Assert.That(result.Result, Is.EqualTo(PathResult.Path),
                "A* should find a path spanning both maps via the portal.");
            var graphUids = result.Path.Select(p => p.GraphUid).Distinct().ToList();
            Assert.That(graphUids, Does.Contain(lowerGrid));
            Assert.That(graphUids, Does.Contain(upperGrid));
        });
    }

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
            var ramp = SEntMan.SpawnEntity("CEZLevelLadderStone",
                new EntityCoordinates(lowerMap, new Vector2(2.5f, 0.5f)));
            xformSys.SetWorldRotation(ramp, Direction.East.ToAngle());
        });

        await RunTicksSync(90);

        // lowApproach = rampTile + downhill(East) = (3,0); landing on upper = rampTile = (2,0).
        var start = new EntityCoordinates(lowerMap, new Vector2(4.5f, 0.5f));
        var end = new EntityCoordinates(upperMap, new Vector2(0.5f, 0.5f));

        Task<PathResultEvent> pathTask = null!;
        await Server.WaitPost(() =>
        {
            pathTask = pathfinding.GetPath(start, end, 0.5f, 0, 0, CancellationToken.None);
        });
        await RunTicksSync(60);

        await Server.WaitAssertion(() =>
        {
            Assert.That(pathTask.IsCompletedSuccessfully, Is.True);
            var result = pathTask.GetAwaiter().GetResult();
            Assert.That(result.Result, Is.EqualTo(PathResult.Path),
                "Ramp portal should let A* path from the lower level to the upper level.");
            var graphUids = result.Path.Select(p => p.GraphUid).Distinct().ToList();
            Assert.That(graphUids, Does.Contain(lowerMap));
            Assert.That(graphUids, Does.Contain(upperMap));
        });
    }

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
            var ramp = SEntMan.SpawnEntity("CEZLevelLadderStone",
                new EntityCoordinates(lowerMap, new Vector2(3.5f, 0.5f)));
            xformSys.SetWorldRotation(ramp, Direction.East.ToAngle());

            // Prey on the upper level; hunter on the lower level near the ramp's low side.
            SEntMan.SpawnEntity("CEMobHuman", new EntityCoordinates(upperMap, new Vector2(1.5f, 0.5f)));
            hunter = SEntMan.SpawnEntity("CEMobRat", new EntityCoordinates(lowerMap, new Vector2(5.5f, 0.5f)));
        });

        // Build navmesh / portal, let the mob initialize.
        await RunTicksSync(90);

        // Give the GOAP agent time to plan and the NPC time to walk up the ramp.
        await RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            var hunterXform = SEntMan.GetComponent<TransformComponent>(hunter);
            Assert.That(hunterXform.MapUid, Is.EqualTo(upperMap),
                "Hostile mob should have walked the ramp up to the prey's level.");
        });
    }
}
