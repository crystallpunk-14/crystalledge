using Content.Shared._CE.Health;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CE.ContactDamage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CEContactDamageComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public CEDamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public TimeSpan ContactDamageInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan NextDamageTime = TimeSpan.Zero;

    [DataField]
    public float PushForce = 10f;
}
