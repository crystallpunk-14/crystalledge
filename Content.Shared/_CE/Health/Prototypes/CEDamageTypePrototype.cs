using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Health.Prototypes;

[Prototype("CEDamageType")]
public sealed partial class CEDamageTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
