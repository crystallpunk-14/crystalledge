using Robust.Shared.GameStates;

namespace Content.Shared._CE.Water;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEWaterDistortionComponent : Component
{
    /// <summary>
    /// Intensity of the water distortion effect, from 0 (none) to 1 (maximum).
    /// Encoded in the mask texture red channel and read by the shader.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity = 1f;
}
