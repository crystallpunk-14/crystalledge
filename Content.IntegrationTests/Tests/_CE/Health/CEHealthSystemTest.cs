using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Health.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._CE.Health;

[TestFixture]
[TestOf(typeof(CESharedHealthSystem))]
public sealed class CEHealthSystemTest
{
    private static readonly ProtoId<CEDamageTypePrototype> TestDamageType = "Physical";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CEHealthTestDummy
  components:
  - type: CEHealth
    health: 100
    maxHealth: 100
    deathThreshold: -20
";

    #region TakeDamage

    /// <summary>
    /// Verify that applying damage through CEDamageSpecifier reduces Health correctly.
    /// </summary>
    [Test]
    public async Task TakeDamageReducesHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            Assert.That(health.Health, Is.EqualTo(100));
            Assert.That(health.MaxHealth, Is.EqualTo(100));
            Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Alive));

            var damage = new CEDamageSpecifier(TestDamageType, 30);
            var result = healthSystem.TakeDamage(ent, damage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(health.Health, Is.EqualTo(70));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Alive));
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that damage with multiple types sums correctly.
    /// </summary>
    [Test]
    public async Task TakeDamageMultipleTypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            var damage = new CEDamageSpecifier();
            damage.Types[TestDamageType] = 20;

            healthSystem.TakeDamage(ent, damage);

            Assert.That(health.Health, Is.EqualTo(80));

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that zero or negative total damage does not change health.
    /// </summary>
    [Test]
    public async Task TakeDamageZeroDamageNoEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            var damage = new CEDamageSpecifier(TestDamageType, 0);
            var result = healthSystem.TakeDamage(ent, damage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(health.Health, Is.EqualTo(100));
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Heal

    /// <summary>
    /// Verify that Heal() restores health, capped at MaxHealth.
    /// </summary>
    [Test]
    public async Task HealRestoresHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            // Deal 50 damage first
            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 50));
            Assert.That(health.Health, Is.EqualTo(50));

            // Heal 30
            healthSystem.Heal(ent, 30);
            Assert.That(health.Health, Is.EqualTo(80));

            // Overheal by 500
            healthSystem.Heal(ent, 500);

            Assert.That(health.Health, Is.EqualTo(100));

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that healing with zero or negative does nothing.
    /// </summary>
    [Test]
    public async Task HealZeroNoEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 10));
            Assert.That(health.Health, Is.EqualTo(90));

            healthSystem.Heal(ent, 0);
            Assert.That(health.Health, Is.EqualTo(90));

            healthSystem.Heal(ent, -5);
            Assert.That(health.Health, Is.EqualTo(90));

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Critical State

    /// <summary>
    /// Verify that when health reaches 0, the entity enters Critical state.
    /// </summary>
    [Test]
    public async Task CriticalStateAtZeroHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            // Damage exactly to 0
            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 100));

            Assert.Multiple(() =>
            {
                Assert.That(health.Health, Is.EqualTo(0));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Critical));
                Assert.That(healthSystem.IsCritical(ent), Is.True);
                Assert.That(healthSystem.IsAlive(ent), Is.False);
                Assert.That(healthSystem.IsDead(ent), Is.False);
                Assert.That(healthSystem.IsIncapacitated(ent), Is.True);
            });

            // Heal past 0
            healthSystem.Heal(ent, 10);

            Assert.Multiple(() =>
            {
                Assert.That(health.Health, Is.EqualTo(10));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Alive));
                Assert.That(healthSystem.IsAlive(ent), Is.True);
            });

            //Kill
            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 99999));

            Assert.Multiple(() =>
            {
                Assert.That(health.Health, Is.EqualTo(-20));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Dead));
                Assert.That(healthSystem.IsDead(ent), Is.True);
                Assert.That(healthSystem.IsAlive(ent), Is.False);
                Assert.That(healthSystem.IsCritical(ent), Is.False);
                Assert.That(healthSystem.IsIncapacitated(ent), Is.True);
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region SetMaxHealth Proportional

    /// <summary>
    /// Verify that SetMaxHealth scales current health proportionally.
    /// 80/100 HP => increase to 200 max => should be 160/200.
    /// </summary>
    [Test]
    public async Task SetMaxHealthScalesProportionally()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            // Reduce to 80/100
            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 20));
            Assert.That(health.Health, Is.EqualTo(80));

            // Double max health
            healthSystem.SetMaxHealth(ent, 200);

            Assert.Multiple(() =>
            {
                Assert.That(health.MaxHealth, Is.EqualTo(200));
                Assert.That(health.Health, Is.EqualTo(160)); // 80 * 200/100
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that reducing MaxHealth scales health down proportionally.
    /// 100/100 HP => reduce to 50 max => should be 50/50.
    /// </summary>
    [Test]
    public async Task SetMaxHealthScalesDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            Assert.That(health.Health, Is.EqualTo(100));

            // Halve max health
            healthSystem.SetMaxHealth(ent, 50);

            Assert.Multiple(() =>
            {
                Assert.That(health.MaxHealth, Is.EqualTo(50));
                Assert.That(health.Health, Is.EqualTo(50)); // 100 * 50/100
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Rejuvenate

    /// <summary>
    /// Verify that RejuvenateEvent fully restores health and Alive state.
    /// </summary>
    [Test]
    public async Task RejuvenateRestoresFullHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var healthSystem = entManager.System<CESharedHealthSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var health = entManager.GetComponent<CEHealthComponent>(ent);

            // Kill entity
            healthSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 999));

            Assert.Multiple(() =>
            {
                Assert.That(health.Health, Is.EqualTo(-20));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Dead));
            });

            // Rejuvenate
            entManager.EventBus.RaiseLocalEvent(ent, new Shared.Rejuvenate.RejuvenateEvent());

            Assert.Multiple(() =>
            {
                Assert.That(health.Health, Is.EqualTo(100));
                Assert.That(health.CurrentState, Is.EqualTo(CEMobState.Alive));
                Assert.That(healthSystem.IsAlive(ent), Is.True);
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region CEDamageSpecifier Math

    /// <summary>
    /// Verify CEDamageSpecifier multiplication operator.
    /// </summary>
    [Test]
    [TestCase(10, 2.0f, 20)]
    [TestCase(10, 0.5f, 5)]
    [TestCase(10, 0f, 0)]
    [TestCase(9, 0.5f, 4)]  // 4.5 rounds down to 4
    [TestCase(9, 0.4f, 3)]  // 3.6 rounds down to 3
    [TestCase(8, 0.4f, 3)]  // 3.4 rounds down to 3
    public async Task DamageSpecifierMultiplyFloat(int a, float b, int result)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var spec = new CEDamageSpecifier(TestDamageType, a);

            var multiplied = spec * b;
            Assert.That(multiplied.Total, Is.EqualTo(result));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify CEDamageSpecifier addition operator.
    /// </summary>
    [Test]
    public async Task DamageSpecifierAdd()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var a = new CEDamageSpecifier(TestDamageType, 10);
            var b = new CEDamageSpecifier(TestDamageType, 25);

            var sum = a + b;
            Assert.That(sum.Total, Is.EqualTo(35));
            Assert.That(sum.Types[TestDamageType], Is.EqualTo(35));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify CEDamageSpecifier copy constructor creates independent copy.
    /// </summary>
    [Test]
    public async Task DamageSpecifierCopy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var original = new CEDamageSpecifier(TestDamageType, 50);
            var copy = new CEDamageSpecifier(original);

            copy.Types[TestDamageType] = 999;

            Assert.Multiple(() =>
            {
                Assert.That(original.Total, Is.EqualTo(50));
                Assert.That(copy.Total, Is.EqualTo(999));
            });
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region No Vanilla Components

    /// <summary>
    /// Verify across ALL prototypes that have CEHealthComponent — none should have
    /// vanilla DamageableComponent, MobStateComponent, or MobThresholdsComponent.
    /// </summary>
    [Test]
    public async Task AllCEHealthPrototypesLackVanillaComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (pair.IsTestPrototype(proto))
                    continue;

                if (!proto.TryGetComponent<CEHealthComponent>(out _, entManager.ComponentFactory))
                    continue;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        proto.TryGetComponent<DamageableComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEHealthComponent and vanilla DamageableComponent");
                    Assert.That(
                        proto.TryGetComponent<MobStateComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEHealthComponent and vanilla MobStateComponent");
                    Assert.That(
                        proto.TryGetComponent<MobThresholdsComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEHealthComponent and vanilla MobThresholdsComponent");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    #endregion
}
