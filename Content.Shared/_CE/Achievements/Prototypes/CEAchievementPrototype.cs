using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Achievements.Prototypes;

[Prototype("achievement")]
public sealed partial class CEAchievementPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name of the achievement.
    /// </summary>
    [DataField]
    public LocId Name = string.Empty;

    /// <summary>
    /// Description of the achievement.
    /// </summary>
    [DataField]
    public LocId Description = string.Empty;
}
