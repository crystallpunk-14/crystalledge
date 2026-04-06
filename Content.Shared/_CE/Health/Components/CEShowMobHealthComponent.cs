using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// When attached to an entity (directly or via equipment), enables the CE health bar overlay
/// that shows health bars above mobs with <see cref="CEMobStateComponent"/> or GOAP AI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEShowMobHealthComponent : Component;
