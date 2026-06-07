using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.StatusEffects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._CE.StatusEffects;

[TestFixture]
[TestOf(typeof(CEInnateStatusEffectSystem))]
public sealed class CEStatusEffectsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CEInnateStatusEffectTestDummy
  components:
  - type: CEDamageable
  - type: CEMobState
    baseMaxHealth: 100
  - type: CEInnateStatusEffect
    components:
    - type: CEBonusHealth
      flatHealthBonus: 50
";

    #region CEInnateStatusEffect

    /// <summary>
    /// Verify that <see cref="CEInnateStatusEffectComponent"/> applies its components to the
    /// status effect entity on MapInit, and that removing the component cleans up the effect.
    /// </summary>
    [Test]
    public async Task InnateStatusEffect_AppliesAndRemovesBonusHealth()
    {
        EntityUid ent = default;

        await Server.WaitAssertion(() =>
        {
            ent = SSpawn("CEInnateStatusEffectTestDummy");
            var mobState = SComp<CEMobStateComponent>(ent);

            // CEBonusHealth (+50) should have been applied at MapInit
            Assert.That(mobState.CriticalThreshold, Is.EqualTo(150),
                "CriticalThreshold should be BaseMaxHealth(100) + FlatHealthBonus(50) after innate effect applies");

            SEntMan.RemoveComponent<CEInnateStatusEffectComponent>(ent);
        });

        // Status effect entity deletion is deferred; wait one tick for cleanup to complete
        await Server.WaitRunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var mobState = SComp<CEMobStateComponent>(ent);
            Assert.That(mobState.CriticalThreshold, Is.EqualTo(100),
                "CriticalThreshold should revert to BaseMaxHealth(100) after innate effect is removed");
        });
    }

    #endregion
}
