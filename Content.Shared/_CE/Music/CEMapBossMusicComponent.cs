using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Music;

/// <summary>
/// Placed on a map entity to mark it as a boss arena and drive boss music playback on the client.
/// The server flips <see cref="State"/> in response to boss battle start/end events.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEMapBossMusicComponent : Component
{
    /// <summary>
    /// Boss music prototype to play. Null disables boss music for this map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CEBossMusicPrototype>? Music;

    /// <summary>
    /// Current encounter stage. Driven server-side; clients read it to pick the playback layer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CEBossMusicState State = CEBossMusicState.Prelude;
}

[Serializable, NetSerializable]
public enum CEBossMusicState : byte
{
    Prelude = 0,
    Battle = 1,
    Victory = 2,
}
