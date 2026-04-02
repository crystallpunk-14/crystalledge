using Robust.Shared.GameStates;

namespace Content.Shared._CE.Water;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEWaterDistortionComponent : Component
{
    [DataField]
    public float Strength = 0.06f;

    [DataField]
    public float Scale = 1.5f;

    [DataField]
    public float Speed = 0.2f;
}
