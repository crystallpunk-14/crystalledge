using Content.Shared._CE.Health.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.HealthExaminable;

[RegisterComponent, Access(typeof(CEHealthExaminableComponent))]
public sealed partial class CEHealthExaminableComponent : Component
{
    // REVIEW: could be interesting to expose this to prototypes; would allow for different thresholds for different mobs.
    public List<FixedPoint2> Thresholds = new()
        { FixedPoint2.New(100), FixedPoint2.New(90), FixedPoint2.New(70), FixedPoint2.New(40), FixedPoint2.New(10) };

    /// <summary>
    ///     Health examine text is automatically generated through creating loc string IDs, in the form:
    ///     `health-examinable-[prefix]-[type]-[threshold]`
    ///     This part determines the prefix.
    /// </summary>
    [DataField]
    public string LocPrefix = "default";
}
