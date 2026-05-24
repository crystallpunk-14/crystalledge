using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Bonfire;

/// <summary>
/// Marks an entity as a bonfire that dungeon players can interact with.
/// Each player may use the bonfire exactly once; on use, a
/// <see cref="CEBonfireRestoredEvent"/> is raised on the player entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CEBonfireComponent : Component
{
    /// <summary>
    /// Player entities that have already used this bonfire.
    /// Networked so the client can update the sprite overlay when the local player is in the list.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> UsedBy = new();

    /// <summary>
    /// Key of the sprite layer to hide once the local player has used this bonfire.
    /// </summary>
    [DataField]
    public string UsedOverlayLayer = "used";

    /// <summary>
    /// Entity prototype spawned at the player's position when the bonfire heals them.
    /// </summary>
    [DataField]
    public EntProtoId? HealVfx = "CEEffectHealingGeneric";

    /// <summary>
    /// Sound played at the player's position when the bonfire heals them.
    /// </summary>
    [DataField]
    public SoundSpecifier? HealSound = new SoundCollectionSpecifier("CECrystalDings");
}
