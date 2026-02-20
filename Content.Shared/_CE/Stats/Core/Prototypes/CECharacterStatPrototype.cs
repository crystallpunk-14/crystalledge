using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Stats.Core.Prototypes;

/// <summary>
/// Defines a character stat prototype (strength, dexterity, vitality, etc.).
/// </summary>
[Prototype("characterStat")]
public sealed partial class CECharacterStatPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name of the stat for display to the player.
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// Localized description of the stat explaining its effect on gameplay.
    /// </summary>
    [DataField(required: true)]
    public LocId Desc = string.Empty;

    /// <summary>
    /// Icon sprite for the stat to display in the interface.
    /// </summary>
    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
