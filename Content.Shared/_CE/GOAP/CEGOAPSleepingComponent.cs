using Robust.Shared.GameStates;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Marker component for sleeping GOAP NPCs. While present, prevents the entity from
/// being woken via <see cref="CECheckGOAPAwakeEvent"/>. Must be removed explicitly
/// by <see cref="CEGOAPSleepingSystem"/> when a wake trigger fires (damage, proximity, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEGOAPSleepingComponent : Component
{
    /// <summary>
    /// Radius within which a player's presence will wake this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WakeRadius = 5f;
}
