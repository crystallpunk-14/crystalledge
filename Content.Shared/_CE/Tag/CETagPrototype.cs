using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Tag;

/// <summary>
/// Prototype representing a tag in YAML.
/// Meant to only have an ID property, as that is the only thing that
/// gets saved in TagComponent.
/// </summary>
[Prototype("CETag")]
public sealed partial class CETagPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField(required: true)]
    public LocId Name = string.Empty;
}
