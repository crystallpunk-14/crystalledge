using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Stores accumulated damage per type for an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CESharedDamageableSystem))]
public sealed partial class CEDamageableComponent : Component
{
    [DataField, ViewVariables]
    public CEDamageSpecifier Damage = new();
}

[Serializable, NetSerializable]
public sealed class CEDamageableComponentState : ComponentState
{
    public CEDamageSpecifier Damage = new();
}
