namespace Content.Shared._CE.StatusEffects.Pacifism;

/// <summary>
/// Placed on the status-effect entity spawned by the stack system.
/// When active, <see cref="CEPacifismSystem"/> blocks combat actions on the target player
/// via relayed attempt events.
/// </summary>
[RegisterComponent]
public sealed partial class CEPacifismEffectComponent : Component;
