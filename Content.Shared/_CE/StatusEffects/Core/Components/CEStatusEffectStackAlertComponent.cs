using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects.Core.Components;

/// <summary>
/// Shows alerts based on effect stack
/// </summary>
[RegisterComponent]
public sealed partial class CEStatusEffectStackAlertComponent : Component
{
    /// <summary>
    /// Category of showed alerts
    /// </summary>
    [DataField(required: true)]
    public ProtoId<AlertCategoryPrototype> AlertsCategory;

    /// <summary>
    /// Stack: Alert
    /// Leave alert null if you don't want to show any alert
    /// Alerts must be in same group
    /// </summary>
    [DataField(required: true)]
    public SortedDictionary<int, ProtoId<AlertPrototype>>  Alerts;
}
