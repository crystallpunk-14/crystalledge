using Content.Shared._CE.EntityEffect;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.EphemeralCollectable;

/// <summary>
/// An entity that can be "collected" by each dungeon player independently.
/// Existing as a single shared world entity, it grants its <see cref="Effects"/>
/// to every <see cref="Content.Shared._CE.Procedural.Components.CEDungeonPlayerComponent"/>
/// that touches it (once per player).
/// On the client, the entity becomes locally invisible for players who already collected it,
/// even though it still exists on the server for everyone else.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CEEphemeralCollectableComponent : Component
{
    /// <summary>
    /// Effects applied to a player on first contact.
    /// </summary>
    [DataField(required: true)]
    public List<CEEntityEffect> Effects = new();

    /// <summary>
    /// Players (entities) that have already collected this from their perspective.
    /// Server-authoritative, networked so each client can hide it locally.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> CollectedBy = new();
}
