using Robust.Shared.Prototypes;

namespace Content.Shared._CE.GOAP;

/// <summary>
/// Defines a named boolean condition used in GOAP world state, goals, and actions.
/// </summary>
[Prototype("ceGoapCondition")]
public sealed partial class CEGOAPConditionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
