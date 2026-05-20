using Content.Server._CE.GOAP;
using Content.Shared._CE.Animation.Core;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAPAlarm;

public sealed partial class CEGOAPAlarmSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CESharedAnimationActionSystem _animation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlarmComponent, CETargetChangedEvent>(OnChangeTarget);
        SubscribeLocalEvent<CEGOAPAlarmAnimationComponent, CETargetChangedEvent>(OnAnimationChangeTarget);

        SubscribeLocalEvent<CEAlarmOnSpawnComponent, MapInitEvent>(OnAlarmOnSpawn);
    }

    private void OnAnimationChangeTarget(Entity<CEGOAPAlarmAnimationComponent> ent, ref CETargetChangedEvent args)
    {
        if (args.NewTarget is null)
            return;

        if (_timing.CurTime > ent.Comp.LastAlarm + ent.Comp.Cooldown)
            _animation.TryPlayAnimationToEntity(ent, ent.Comp.Animation, args.NewTarget.Value, forceCancel: true);

        ent.Comp.LastAlarm = _timing.CurTime;

        Alarm(Transform(ent).Coordinates, args.NewTarget.Value, ent.Comp.Radius);
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

    private void Alarm(EntityCoordinates source, EntityUid target, float radius)
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
