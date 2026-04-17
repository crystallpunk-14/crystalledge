using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._CE.MeleeWeapon;

[TestFixture]
public sealed class CERatAttackTest : GameTest
{
    [Test]
    public async Task RatAttacksHumanOnTileGrid()
    {
        // Create test map
        var map = await Pair.CreateTestMap();

        // Set up 5x5 tile platform
        await Server.WaitPost(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            var tileMan = Server.ResolveDependency<ITileDefinitionManager>();
            var tile = new Tile(tileMan["Plating"].TileId);

            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 5; y++)
                {
                    mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), tile);
                }
            }
        });

        // Spawn CEHuman at center, CEMobRat on adjacent tile
        var humanCoords = new EntityCoordinates(map.Grid.Owner, new Vector2(2.5f, 2.5f));
        var ratCoords = new EntityCoordinates(map.Grid.Owner, new Vector2(3.5f, 2.5f));

        var human = await SpawnAtPosition("CEMobHuman", humanCoords);
        var rat = await SpawnAtPosition("CEMobFlem", ratCoords);

        // Let physics and GOAP initialize
        await RunTicksSync(5);

        // Get initial health
        var initialDamage = 0;
        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.GetComponent<CEDamageableComponent>(human);
            initialDamage = damageable.Damage.Total;
        });


        await RunSeconds(1);

        await SpawnAtPosition("CEAlarmInRange5", humanCoords);

        await RunSeconds(5);

        // Assert human took damage, GOAP is active, and rat has target
        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.GetComponent<CEDamageableComponent>(human);
            Assert.That(damageable.Damage.Total, Is.GreaterThan(initialDamage),
                "CEHuman should have taken damage from the enemy");

            Assert.That(SEntMan.HasComponent<CEActiveGOAPComponent>(rat),
                "Enemy GOAP should be active");

            var goap = SEntMan.GetComponent<CEGOAPComponent>(rat);
            Assert.That(goap.Targets.ContainsKey("enemy"),
                "Enemy should have an enemy target");
        });
    }
}
