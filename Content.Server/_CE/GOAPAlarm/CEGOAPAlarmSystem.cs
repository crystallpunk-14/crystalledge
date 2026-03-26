using Content.Server._CE.GOAP;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAPAlarm;

public sealed partial class CEGOAPAlarmSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlarmComponent, CETargetChangedEvent>(OnChangeTarget);
        SubscribeLocalEvent<CEAlarmOnSpawnComponent, MapInitEvent>(OnAlarmOnSpawn);
    }

    private void OnAlarmOnSpawn(Entity<CEAlarmOnSpawnComponent> ent, ref MapInitEvent args)
    {
        Alarm(Transform(ent).Coordinates, ent.Owner, ent.Comp.Radius);
    }

    private void OnChangeTarget(Entity<CEGOAPAlarmComponent> ent, ref CETargetChangedEvent args)
    {
        if (args.NewTarget is null)
            return;

        if (_timing.CurTime > ent.Comp.LastAlarm + ent.Comp.Cooldown)
        {
            var vfx = SpawnAttachedTo(ent.Comp.AlarmVFX, Transform(ent).Coordinates);
            _transform.SetParent(vfx, ent);
            _audio.PlayPvs(ent.Comp.Sound, ent);
        }

        ent.Comp.LastAlarm = _timing.CurTime;

        Alarm(Transform(ent).Coordinates, args.NewTarget.Value, ent.Comp.Radius);
    }

    public void Alarm(EntityCoordinates source, EntityUid target, float radius)
    {
        RaiseLocalEvent(new CEGOAPAlarmEvent(source, target, radius));
    }
}

/// <summary>
/// An event broadcast to alert GOAP agents within a radius
/// </summary>
public sealed class CEGOAPAlarmEvent(EntityCoordinates source, EntityUid target, float radius) : EntityEventArgs
{
    public EntityCoordinates Source = source;
    public EntityUid Target = target;
    public float Radius = radius;
}
