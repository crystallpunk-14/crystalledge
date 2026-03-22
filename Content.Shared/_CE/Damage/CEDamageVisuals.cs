using Robust.Shared.Serialization;

namespace Content.Shared._CE.Damage;

/// <summary>
/// Appearance data keys used by <c>CEDamageVisualsSystem</c> on the client.
/// </summary>
[Serializable, NetSerializable]
public enum CEDamageVisuals : byte
{
    /// <summary>
    /// Float 0–1. Fraction of damage relative to the critical threshold.
    /// </summary>
    DamageFraction,
}
