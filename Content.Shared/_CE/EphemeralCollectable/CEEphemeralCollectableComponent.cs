using Content.Shared._CE.EntityEffect;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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

    /// <summary>
    /// Optional client-side VFX entity spawned at the collectable's position the moment
    /// the local player collects it. Client-only spawn → instant feedback, no server lag.
    /// </summary>
    [DataField]
    public EntProtoId? CollectVfx;

    /// <summary>
    /// Optional sound played predictively at the collectable's position when a player collects it.
    /// Predicted = local player hears it instantly via prediction; the server confirms for everyone else.
    /// </summary>
    [DataField]
    public SoundSpecifier? CollectSound;

    /// <summary>
    /// Grace period after spawn during which the collectable cannot be picked up. Gives the
    /// client time to receive the entity state, run scatter animations, and lets the player
    /// actually see the drop instead of auto-collecting it on contact a single tick later.
    /// </summary>
    [DataField]
    public TimeSpan CollectionDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Server-authoritative timestamp at which this collectable becomes collectable. Set
    /// during map init to <c>CurTime + CollectionDelay</c>. Networked so prediction agrees
    /// with the server about the grace cutoff.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CollectableAt;
}

