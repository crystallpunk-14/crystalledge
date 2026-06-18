using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.CollectOnContact;

/// <summary>
/// When this entity collides with another entity, the system recursively searches
/// that entity's containers for a <see cref="CECollectOnContactTargetComponent"/>
/// with a matching <see cref="StorageTag"/> and auto-inserts this entity into it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CECollectOnContactComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<TagPrototype> StorageTag;
}
