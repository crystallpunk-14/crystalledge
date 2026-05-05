using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Music;

/// <summary>
/// Placed on a map entity to specify which ambient music prototype should play
/// when the local player is on that map.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEMapAmbientMusicThemeComponent : Component
{
    /// <summary>
    /// The ambient music prototype to play on this map.
    /// Null means no CE ambient music on this map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CEAmbientMusicPrototype>? Theme;
}
