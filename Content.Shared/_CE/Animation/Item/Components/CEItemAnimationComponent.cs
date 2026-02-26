using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Item.Components;

/// <summary>
/// Using this item in combat mode triggers action animations on the character.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CESharedItemAnimationSystem))]
public sealed partial class CEItemAnimationComponent : Component
{
    /// <summary>
    /// Mapping from input button to attack action prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEUseType, List<ProtoId<CEAnimationActionPrototype>>> Animations = new();

    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Using = false;

    [DataField]
    public TimeSpan ComboResetTime = TimeSpan.FromSeconds(0.5f);
}

/// <summary>
/// Which input button binding triggers the attack.
/// </summary>
public enum CEUseType : byte
{
    Primary,
    Secondary,
}
