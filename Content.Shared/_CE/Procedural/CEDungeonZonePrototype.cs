using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Procedural;

[Prototype("dungeonZone")]
public sealed partial class CEDungeonZonePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
}
