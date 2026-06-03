using Content.Shared._CE.GOAP.Selectors;

namespace Content.Shared._CE.GOAP.Sensors;

public interface ICEGOAPSensorBase<T> where T: CEGOAPSensor
{
    List<T> Sensors { get; set; }
}

[DataDefinition]
public abstract partial class CEGOAPSensor
{
    [DataField(required: true)]
    public string ConditionKey = string.Empty;

    [DataField(required: true)]
    public CEGOAPTargetSelector Selector = default!;
}
