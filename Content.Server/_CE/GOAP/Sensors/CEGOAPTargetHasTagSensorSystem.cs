using Content.Shared._CE.GOAP;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GOAP.Sensors;

/// <summary>
/// Checks if the Target has a specific Tag.
/// </summary>
public sealed partial class CEGOAPTargetHasTagSensor : CEGOAPSensorBase<CEGOAPTargetHasTagSensor>
{
    /// <summary>
    /// A tag that Target must have.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TagPrototype> TagName;
}

public sealed partial class CEGOAPTargetHasTagSensorSystem : CEGOAPSensorSystem<CEGOAPTargetHasTagSensor>
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    protected override void OnSensorUpdate(Entity<CEGOAPComponent> ent, ref CEGOAPSensorUpdateEvent<CEGOAPTargetHasTagSensor> args)
    {
        var target = GetTarget(ent.Comp, args.Sensor.TargetProviderKey);
        if (target == null)
        {
            SetState(ref args, false);
            return;
        }

        SetState(ref args, _tagSystem.HasTag(target.Value, args.Sensor.TagName));
    }
}
