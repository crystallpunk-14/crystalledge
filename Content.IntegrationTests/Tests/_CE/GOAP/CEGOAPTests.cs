using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Mana.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._CE.GOAP;

[TestFixture]
public sealed class CEGOAPTests : GameTest
{
    [SidedDependency(Side.Server)]
    private readonly CESharedMagicEnergySystem _mana = default!;

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
        - !type:ManaPercentCondition
          max: 0.5
      preconditions:
        EnemyVisible: true
      effects:
        EnemyInMeleeRange: true
      cost: 2
    - !type:CEGOAPMeleeAttackAction
      selector: !type:CEGOAPSelectorNearestEnemy
        conditions:
        - !type:ManaPercentCondition
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
    /// Condition selector: mob with ManaPercentCondition max=0.5 prefers the low-mana human
    /// over the closer full-mana human.
    ///
    /// Layout (top view):
    ///   Mob               at (2.5, 2.5)
    ///   Human1 (full mana) at (3.5, 2.5) — 1 tile RIGHT, CLOSER
    ///   Human2 (no mana)   at (4.5, 2.5) — 2 tiles RIGHT, FARTHER
    ///
    /// Both humans have CEMagicEnergyContainer (from CEMobMagical via CEBaseSpeciesMob).
    /// Human1 keeps full mana (100%) — fails ManaPercentCondition max=0.5.
    /// Human2 has mana drained to 0% — passes ManaPercentCondition max=0.5.
    ///
    /// Without the condition the mob would pick Human1 (nearest).
    /// With the condition the mob must skip Human1 and attack Human2.
    /// Test passes if Human2 ends up with more damage than Human1.
    /// </summary>
    [Test]
    public async Task GOAPConditionSelectorTest()
    {
        var map = await Pair.CreateTestMap();
        await SetupTileGrid(map);

        var mobCoords    = new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f));
        var human1Coords = new EntityCoordinates(map.Grid.Owner, new Vector2(3.5f, 2.5f)); // closer, full mana
        var human2Coords = new EntityCoordinates(map.Grid.Owner, new Vector2(4.5f, 2.5f)); // farther, no mana

        var human1 = await SpawnAtPosition("CEMobHuman", human1Coords);
        var human2 = await SpawnAtPosition("CEMobHuman", human2Coords);
        var mob    = await SpawnAtPosition("CEMobFlemConditionTest", mobCoords);

        await RunTicksSync(5);

        // Drain all mana from human2 so the ratio is 0% — passes ManaPercentCondition max=0.5
        await Server.WaitPost(() =>
        {
            _mana.Take(human2, 100);
        });

        await RunSeconds(1);
        await SpawnAtPosition("CEAlarmInRange5", mobCoords);
        await RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            var human1Damage = SEntMan.GetComponent<CEDamageableComponent>(human1).Damage.Total;
            var human2Damage = SEntMan.GetComponent<CEDamageableComponent>(human2).Damage.Total;

            // Mob must have attacked human2 (no mana) and dealt more damage to it than to human1 (full mana).
            // Without the condition the mob would prefer human1 (nearest), making human1Damage > human2Damage.
            Assert.That(human2Damage, Is.GreaterThan(human1Damage),
                "Mob should have dealt more damage to the low-mana human than to the full-mana human");
            Assert.That(human2Damage, Is.GreaterThan(0),
                "Mob should have attacked the low-mana human");
            Assert.That(SEntMan.HasComponent<CEActiveGOAPComponent>(mob),
                "Mob GOAP should be active");
        });
    }
}
