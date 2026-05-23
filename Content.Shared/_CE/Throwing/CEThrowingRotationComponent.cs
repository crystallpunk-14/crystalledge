using Robust.Shared.GameStates;

namespace Content.Shared._CE.Throwing;

/// <summary>
/// Controls the rotation behavior of this entity when thrown.
/// Allows configuring a custom angular velocity and starting angle offset.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEThrowingRotationComponent : Component
{
    [DataField, AutoNetworkedField]
    public float? RotationSpeed = null;

    [DataField, AutoNetworkedField]
    public Angle StartAngle = Angle.Zero;
}
