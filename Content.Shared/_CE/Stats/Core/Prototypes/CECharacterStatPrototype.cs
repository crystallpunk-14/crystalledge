using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Stats.Core.Prototypes;

/// <summary>
///
/// </summary>
[Prototype("characterStat")]
public sealed partial class CECharacterStatPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public LocId Desc = string.Empty;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
