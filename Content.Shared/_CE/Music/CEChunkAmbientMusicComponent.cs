using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Music;

/// <summary>
/// Placed on a player to carry the ambient music theme of the world chunk they currently stand in.
/// The server updates it as the player crosses chunk boundaries; the client plays it, preferring it
/// over any map-level <see cref="CEMapAmbientMusicThemeComponent"/>. The per-chunk analog of that map
/// component. Null means the current chunk specifies no theme (fall back to the map theme).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CEChunkAmbientMusicComponent : Component
{
    /// <summary>
    /// The ambient music prototype for the player's current chunk, or null for none.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CEAmbientMusicPrototype>? Theme;

    public override bool SendOnlyToOwner => true;
}
