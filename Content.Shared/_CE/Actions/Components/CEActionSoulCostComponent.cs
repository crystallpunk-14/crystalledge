namespace Content.Shared._CE.Actions.Components;

/// <summary>
/// Restricts the use of this action, by spending users souls.
/// </summary>
[RegisterComponent]
public sealed partial class CEActionSoulCostComponent : Component
{
    [DataField(required: true)]
    public int Cost = 1;
}
