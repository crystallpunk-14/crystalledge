using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.Prototypes;

[Prototype("tileEffect")]
public sealed partial class CETileEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
