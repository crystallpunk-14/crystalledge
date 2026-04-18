namespace Content.Shared._CE.StatusEffects.Pacifism;

/// <summary>
/// Placed on the status-effect entity spawned by the stack system.
/// Bridges the effect lifecycle to <see cref="CEPacifismComponent"/> on the target player.
/// </summary>
[RegisterComponent]
public sealed partial class CEPacifismEffectComponent : Component;
