namespace Content.Shared._CE.StatusEffects.Pacifism;

/// <summary>
/// Marker placed on a status-effect entity prototype to flag it as a hostile/negative effect.
/// Pacifism (and similar protective systems) use this to decide whether to block application
/// of the effect on a player target.
/// </summary>
[RegisterComponent]
public sealed partial class CENegativeStatusEffectComponent : Component;
