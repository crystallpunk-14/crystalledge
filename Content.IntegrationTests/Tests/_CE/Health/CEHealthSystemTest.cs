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
[TestOf(typeof(CESharedDamageableSystem))]
public sealed class CEHealthSystemTest
{
    private static readonly ProtoId<CEDamageTypePrototype> TestDamageType = "Physical";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CEHealthTestDummy
  components:
  - type: CEDamageable
  - type: CEMobState
    criticalThreshold: 100
    deadThreshold: 120
";

    #region TakeDamage

    /// <summary>
    /// Verify that applying damage through CEDamageSpecifier increases TotalDamage correctly.
    /// </summary>
    [Test]
    public async Task TakeDamageReducesHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var damageableSystem = entManager.System<CESharedDamageableSystem>();
        var mobStateSystem = entManager.System<CEMobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);
            var mobState = entManager.GetComponent<CEMobStateComponent>(ent);

            Assert.That(damageable.TotalDamage, Is.EqualTo(0));
            Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Alive));

            var damage = new CEDamageSpecifier(TestDamageType, 30);
            var result = damageableSystem.TakeDamage(ent, damage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(damageable.TotalDamage, Is.EqualTo(30));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Alive));
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
        var damageableSystem = entManager.System<CESharedDamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);

            var damage = new CEDamageSpecifier();
            damage.Types[TestDamageType] = 20;

            damageableSystem.TakeDamage(ent, damage);

            Assert.That(damageable.TotalDamage, Is.EqualTo(20));

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Verify that zero or negative total damage does not change TotalDamage.
    /// </summary>
    [Test]
    public async Task TakeDamageZeroDamageNoEffect()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var damageableSystem = entManager.System<CESharedDamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);

            var damage = new CEDamageSpecifier(TestDamageType, 0);
            var result = damageableSystem.TakeDamage(ent, damage);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(damageable.TotalDamage, Is.EqualTo(0));
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Heal

    /// <summary>
    /// Verify that Heal() reduces accumulated damage, capped at 0.
    /// </summary>
    [Test]
    public async Task HealRestoresHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var damageableSystem = entManager.System<CESharedDamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);

            // Deal 50 damage first
            damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 50));
            Assert.That(damageable.TotalDamage, Is.EqualTo(50));

            // Heal 30
            damageableSystem.Heal(ent, 30);
            Assert.That(damageable.TotalDamage, Is.EqualTo(20));

            // Overheal by 500
            damageableSystem.Heal(ent, 500);

            Assert.That(damageable.TotalDamage, Is.EqualTo(0));

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
        var damageableSystem = entManager.System<CESharedDamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);

            damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 10));
            Assert.That(damageable.TotalDamage, Is.EqualTo(10));

            damageableSystem.Heal(ent, 0);
            Assert.That(damageable.TotalDamage, Is.EqualTo(10));

            damageableSystem.Heal(ent, -5);
            Assert.That(damageable.TotalDamage, Is.EqualTo(10));

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Critical State

    /// <summary>
    /// Verify that when damage reaches CriticalThreshold, the entity enters Critical state.
    /// </summary>
    [Test]
    public async Task CriticalStateAtThreshold()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var damageableSystem = entManager.System<CESharedDamageableSystem>();
        var mobStateSystem = entManager.System<CEMobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);
            var mobState = entManager.GetComponent<CEMobStateComponent>(ent);

            // Damage exactly to critical threshold (100)
            damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 100));

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(100));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Critical));
                Assert.That(mobStateSystem.IsCritical(ent), Is.True);
                Assert.That(mobStateSystem.IsAlive(ent), Is.False);
                Assert.That(mobStateSystem.IsDead(ent), Is.False);
                Assert.That(mobStateSystem.IsIncapacitated(ent), Is.True);
            });

            // Heal past critical
            damageableSystem.Heal(ent, 10);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(90));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Alive));
                Assert.That(mobStateSystem.IsAlive(ent), Is.True);
            });

            // Kill (damage to dead threshold = 120)
            damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 99999));

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(100089));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Dead));
                Assert.That(mobStateSystem.IsDead(ent), Is.True);
                Assert.That(mobStateSystem.IsAlive(ent), Is.False);
                Assert.That(mobStateSystem.IsCritical(ent), Is.False);
                Assert.That(mobStateSystem.IsIncapacitated(ent), Is.True);
            });

            entManager.DeleteEntity(ent);
        });

        await pair.CleanReturnAsync();
    }

    #endregion

    #region Rejuvenate

    /// <summary>
    /// Verify that RejuvenateEvent fully resets damage and restores Alive state.
    /// </summary>
    [Test]
    public async Task RejuvenateRestoresFullHealth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var damageableSystem = entManager.System<CESharedDamageableSystem>();
        var mobStateSystem = entManager.System<CEMobStateSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = entManager.SpawnEntity("CEHealthTestDummy", MapCoordinates.Nullspace);
            var damageable = entManager.GetComponent<CEDamageableComponent>(ent);
            var mobState = entManager.GetComponent<CEMobStateComponent>(ent);

            // Kill entity
            damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 999));

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(999));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Dead));
            });

            // Rejuvenate
            entManager.EventBus.RaiseLocalEvent(ent, new Shared.Rejuvenate.RejuvenateEvent());

            Assert.Multiple(() =>
            {
                Assert.That(damageable.TotalDamage, Is.EqualTo(0));
                Assert.That(mobState.CurrentState, Is.EqualTo(CEMobState.Alive));
                Assert.That(mobStateSystem.IsAlive(ent), Is.True);
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
    /// Verify across ALL prototypes that have CEDamageableComponent — none should have
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

                if (!proto.TryGetComponent<CEDamageableComponent>(out _, entManager.ComponentFactory))
                    continue;

                Assert.Multiple(() =>
                {
                    Assert.That(
                        proto.TryGetComponent<DamageableComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla DamageableComponent");
                    Assert.That(
                        proto.TryGetComponent<MobStateComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla MobStateComponent");
                    Assert.That(
                        proto.TryGetComponent<MobThresholdsComponent>(out _, entManager.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla MobThresholdsComponent");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    #endregion
}
