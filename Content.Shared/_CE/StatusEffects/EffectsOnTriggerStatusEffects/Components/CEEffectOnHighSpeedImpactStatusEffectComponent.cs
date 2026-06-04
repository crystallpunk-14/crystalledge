using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.StatusEffects.EffectsOnTriggerStatusEffects.Components;

/// <summary>
/// Apply CEEntityEffects when the owner of this status effect collides at or above MinimumSpeed.
/// The effect User is the colliding entity; Target is the other entity in the collision.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnHighSpeedImpactStatusEffectComponent : Component
{
    [DataField]
    public List<CEEntityEffect> Effects = new();

    [DataField]
    public int StackCost = 0;

    /// <summary>Minimum speed (m/s) required to trigger the effects.</summary>
    [DataField]
    public float MinimumSpeed = 20f;

    /// <summary>Cooldown in seconds between effect triggers.</summary>
    [DataField]
    public float DamageCooldown = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? LastHit;

    [DataField]
    public bool ScaleWithStacks = true;
}
