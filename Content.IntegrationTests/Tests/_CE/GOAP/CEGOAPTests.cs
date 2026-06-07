using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._CE.GOAP;

[TestFixture]
public sealed class CEGOAPTests : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly CESharedDamageableSystem _damageable = default!;

    [TestPrototypes]
    private const string TestPrototypes = @"
- type: entity
  parent: CEMobFlem
  id: CEMobFlemConditionTest
  categories: [ ForkFiltered ]
  components:
  - type: CEGOAP
    goals:
    - desiredState:
        EnemyDead: true
      priority: 3
    actions:
    - !type:CEGOAPMoveToTargetAction
      selector: !type:CEGOAPSelectorNearestEnemy
        conditions:
        - !type:HealthPercentCondition
          max: 0.5
      preconditions:
        EnemyVisible: true
      effects:
        EnemyInMeleeRange: true
      cost: 2
    - !type:CEGOAPMeleeAttackAction
      selector: !type:CEGOAPSelectorNearestEnemy
        conditions:
        - !type:HealthPercentCondition
          max: 0.5
      preconditions:
        EnemyInMeleeRange: true
      effects:
        EnemyDead: true
      cost: 1
      useType: Primary
      angleVariation: 5
    - !type:CEGOAPExploreAction
      effects:
        EnemyVisible: true
      cost: 1
      exploreRadius: 8
";

    private async Task SetupTileGrid(TestMapData map, int width = 5, int height = 5)
    {
        await Server.WaitPost(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            var tileMan = Server.ResolveDependency<ITileDefinitionManager>();
            var tile = new Tile(tileMan["Plating"].TileId);
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++)
                    mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), tile);
        });
    }

    /// <summary>
    /// Basic GOAP: hostile mob attacks the nearest human.
    /// </summary>
    [Test]
    public async Task GOAPAttackTest()
    {
        var map = await Pair.CreateTestMap();
        await SetupTileGrid(map);

        var humanCoords = new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f));
        var mobCoords   = new EntityCoordinates(map.Grid.Owner, new Vector2(3.5f, 2.5f));

        var human = await SpawnAtPosition("CEMobHuman", humanCoords);
        var mob   = await SpawnAtPosition("CEMobFlem", mobCoords);

        await RunTicksSync(5);

        var initialDamage = 0;
        await Server.WaitAssertion(() =>
        {
            initialDamage = SEntMan.GetComponent<CEDamageableComponent>(human).Damage.Total;
        });

        await RunSeconds(1);
        await SpawnAtPosition("CEAlarmInRange5", humanCoords);
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.GetComponent<CEDamageableComponent>(human);
            Assert.That(damageable.Damage.Total, Is.GreaterThan(initialDamage),
                "Human should have taken damage from the mob");
            Assert.That(SEntMan.HasComponent<CEActiveGOAPComponent>(mob),
                "Mob GOAP should be active");
            var cache = SEntMan.GetComponent<CEGOAPKnowledgeCacheComponent>(mob);
            Assert.That(cache.Enemies, Is.Not.Empty,
                "Mob should have classified at least one enemy");
        });
    }

    /// <summary>
    /// Condition selector: mob with HealthPercentCondition max=0.5 ignores the closer healthy
    /// human and attacks the farther wounded human instead.
    ///
    /// Layout (top view):
    ///   Human1 (healthy)  at (2.5, 1.5) — 1 tile ABOVE mob, distance 1
    ///   Mob               at (2.5, 2.5)
    ///   Human2 (wounded)  at (4.5, 2.5) — 2 tiles RIGHT of mob, distance 2
    ///
    /// Mob moves right toward Human2. Human1 is perpendicular (above), so it stays
    /// outside the 40° attack arc even when Mob is adjacent to Human2.
    /// </summary>
    [Test]
    public async Task GOAPConditionSelectorTest()
    {
        var map = await Pair.CreateTestMap();
        await SetupTileGrid(map);

        var mobCoords    = new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f));
        var human1Coords = new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 1.5f)); // above, closer
        var human2Coords = new EntityCoordinates(map.Grid.Owner, new Vector2(4.5f, 2.5f)); // right, farther

        var human1 = await SpawnAtPosition("CEMobHuman", human1Coords);
        var human2 = await SpawnAtPosition("CEMobHuman", human2Coords);
        var mob    = await SpawnAtPosition("CEMobFlemConditionTest", mobCoords);

        await RunTicksSync(5);

        // Wound human2 to ~40% HP (max HP = 100, deal 60 damage so ratio = 0.4)
        await Server.WaitPost(() =>
        {
            _damageable.SetDamage(human2, 60);
        });

        var human1InitialDamage = 0;
        await Server.WaitAssertion(() =>
        {
            human1InitialDamage = SEntMan.GetComponent<CEDamageableComponent>(human1).Damage.Total;
        });

        await RunSeconds(1);
        await SpawnAtPosition("CEAlarmInRange5", mobCoords);
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            var human1Damage = SEntMan.GetComponent<CEDamageableComponent>(human1).Damage.Total;
            var human2Damage = SEntMan.GetComponent<CEDamageableComponent>(human2).Damage.Total;

            Assert.That(human1Damage, Is.EqualTo(human1InitialDamage),
                "Healthy human (closer) should not have been attacked by the mob");
            Assert.That(human2Damage, Is.GreaterThan(60),
                "Wounded human should have been attacked by the mob");
            Assert.That(SEntMan.HasComponent<CEActiveGOAPComponent>(mob),
                "Mob GOAP should be active");
        });
    }
}
