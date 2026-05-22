using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Tools;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CELanternComponent : Component
{
    /// <summary>
    /// Whether the lantern light is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Activated = true;

    [DataField]
    public SoundSpecifier TurnOnSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");

    [DataField]
    public SoundSpecifier TurnOffSound = new SoundPathSpecifier("/Audio/Items/flashlight_off.ogg");

    [DataField]
    public LocId VerbToggleOn = "ce-lantern-verb-on";

    [DataField]
    public LocId VerbToggleOff = "ce-lantern-verb-off";
}
