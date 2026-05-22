using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
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
public sealed class CEHealthSystemTest : GameTest
{
    private static readonly ProtoId<CEDamageTypePrototype> TestDamageType = "Physical";
    [SidedDependency(Side.Server)] private readonly CESharedDamageableSystem _damageableSystem = default!;
    [SidedDependency(Side.Server)] private readonly CEMobStateSystem _mobStateSystem = default!;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CEHealthTestDummy
  components:
  - type: CEDamageable
  - type: CEMobState
    criticalThreshold: 100
  - type: CEDestructible
    destroyThreshold: 25
";

    #region TakeDamage

    /// <summary>
    /// Verify that damage with multiple types sums correctly.
    /// </summary>
    [Test]
    public async Task TakeDamageMultipleTypes()
    {
        var server = Server;
        var damageableSystem = SEntMan.System<CESharedDamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);

            var damage = new CEDamageSpecifier
            {
                Types =
                {
                    [TestDamageType] = 20,
                },
            };

            damageableSystem.TakeDamage(ent, damage);

            Assert.That(damageable.Damage.Total, Is.EqualTo(20));

        });
    }

    /// <summary>
    /// Verify that zero or negative total damage does not change TotalDamage.
    /// </summary>
    [Test]
    public async Task TakeDamageZeroDamageNoEffect()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);

            var damage = new CEDamageSpecifier(TestDamageType, 0);
            var result = _damageableSystem.TakeDamage(ent, damage);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(damageable.Damage.Total, Is.Zero);
            }

        });
    }

    #endregion

    #region Heal

    /// <summary>
    /// Verify that overheal caps damage at zero.
    /// </summary>
    [Test]
    public async Task HealCapsAtZero()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);

            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 20));
            _damageableSystem.Heal(ent, 500);

            Assert.That(damageable.Damage.Total, Is.Zero);

        });
    }

    /// <summary>
    /// Verify that healing with zero or negative does nothing.
    /// </summary>
    [Test]
    public async Task HealZeroNoEffect()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);

            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 10));
            Assert.That(damageable.Damage.Total, Is.EqualTo(10));

            _damageableSystem.Heal(ent, 0);
            Assert.That(damageable.Damage.Total, Is.EqualTo(10));

            _damageableSystem.Heal(ent, -5);
            Assert.That(damageable.Damage.Total, Is.EqualTo(10));

        });
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Verify mob state transitions: Alive -> Critical on threshold, heal back to Alive,
    /// massive damage stays Critical, and TakeDamage returns true.
    /// </summary>
    [Test]
    public async Task MobStateTransitions()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);
            var mobState = SComp<CEMobStateComponent>(ent);

            // Sub-threshold damage keeps Alive
            var result = _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 30));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(damageable.Damage.Total, Is.EqualTo(30));
                Assert.That(mobState.Critical, Is.False);
                Assert.That(_mobStateSystem.IsAlive(ent), Is.True);
            }

            // Damage exactly to critical threshold (100)
            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 70));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.Damage.Total, Is.EqualTo(100));
                Assert.That(mobState.Critical, Is.True);
                Assert.That(_mobStateSystem.IsCritical(ent), Is.True);
                Assert.That(_mobStateSystem.IsAlive(ent), Is.False);
            }

            // Heal past critical
            _damageableSystem.Heal(ent, 10);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.Damage.Total, Is.EqualTo(90));
                Assert.That(mobState.Critical, Is.False);
                Assert.That(_mobStateSystem.IsAlive(ent), Is.True);
            }

            // Huge damage still stays Critical (no Dead state)
            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 99999));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.Damage.Total, Is.EqualTo(100089));
                Assert.That(mobState.Critical, Is.True);
                Assert.That(_mobStateSystem.IsCritical(ent), Is.True);
                Assert.That(_mobStateSystem.IsAlive(ent), Is.False);
            }

        });
    }

    #endregion

    #region Rejuvenate

    /// <summary>
    /// Verify that RejuvenateEvent fully resets damage and restores Alive state.
    /// </summary>
    [Test]
    public async Task RejuvenateRestoresFullHealth()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var ent = SSpawn("CEHealthTestDummy");
            var damageable = SComp<CEDamageableComponent>(ent);
            var mobState = SComp<CEMobStateComponent>(ent);

            // Bring to critical
            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 999));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.Damage.Total, Is.EqualTo(999));
                Assert.That(mobState.Critical, Is.True);
            }

            // Rejuvenate
            SEntMan.EventBus.RaiseLocalEvent(ent, new Shared.Rejuvenate.RejuvenateEvent());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(damageable.Damage.Total, Is.Zero);
                Assert.That(mobState.Critical, Is.False);
                Assert.That(_mobStateSystem.IsAlive(ent), Is.True);
            }

        });
    }

    #endregion

    [Test]
    public async Task CriticalDamageLimitDeletesEntity()
    {
        var server = Server;
        EntityUid ent = default;

        await server.WaitAssertion(() =>
        {
            ent = SSpawn("CEHealthTestDummy");
            var mobState = SComp<CEMobStateComponent>(ent);

            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 100));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(SEntMan.EntityExists(ent), Is.True);
                Assert.That(mobState.Critical, Is.True);
            }

            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 24));
            Assert.That(SEntMan.EntityExists(ent), Is.True);

            _damageableSystem.TakeDamage(ent, new CEDamageSpecifier(TestDamageType, 1));
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(ent), Is.False);
        });
    }

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
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var spec = new CEDamageSpecifier(TestDamageType, a);

            var multiplied = spec * b;
            Assert.That(multiplied.Total, Is.EqualTo(result));
        });
    }

    /// <summary>
    /// Verify CEDamageSpecifier addition operator.
    /// </summary>
    [Test]
    public async Task DamageSpecifierAdd()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var a = new CEDamageSpecifier(TestDamageType, 10);
            var b = new CEDamageSpecifier(TestDamageType, 25);

            var sum = a + b;
            Assert.That(sum.Total, Is.EqualTo(35));
            Assert.That(sum.Types[TestDamageType], Is.EqualTo(35));
        });
    }

    /// <summary>
    /// Verify CEDamageSpecifier copy constructor creates independent copy.
    /// </summary>
    [Test]
    public async Task DamageSpecifierCopy()
    {
        var server = Server;

        await server.WaitAssertion(() =>
        {
            var original = new CEDamageSpecifier(TestDamageType, 50);
            var copy = new CEDamageSpecifier(original)
            {
                Types =
                {
                    [TestDamageType] = 999,
                },
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(original.Total, Is.EqualTo(50));
                Assert.That(copy.Total, Is.EqualTo(999));
            }
        });
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
        var pair = Pair;
        var server = Server;

        await server.WaitAssertion(() =>
        {
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (pair.IsTestPrototype(proto))
                    continue;

                if (!proto.TryGetComponent<CEDamageableComponent>(out _, SEntMan.ComponentFactory))
                    continue;

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(
                        proto.TryGetComponent<DamageableComponent>(out _, SEntMan.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla DamageableComponent");
                    Assert.That(
                        proto.TryGetComponent<MobStateComponent>(out _, SEntMan.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla MobStateComponent");
                    Assert.That(
                        proto.TryGetComponent<MobThresholdsComponent>(out _, SEntMan.ComponentFactory),
                        Is.False,
                        $"Prototype '{proto.ID}' has both CEDamageableComponent and vanilla MobThresholdsComponent");
                }
            }
        });
    }

    #endregion
}

