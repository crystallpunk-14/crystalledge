using Content.Server._CE.GOAP;
using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.Animation.Core;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAPAlarm;

public sealed partial class CEGOAPAlarmSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CESharedAnimationActionSystem _animation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEGOAPAlarmComponent, CEGOAPEnemyAcquiredEvent>(OnEnemyAcquired);
        SubscribeLocalEvent<CEGOAPAlarmAnimationComponent, CEGOAPEnemyAcquiredEvent>(OnAnimationEnemyAcquired);

        SubscribeLocalEvent<CEAlarmOnSpawnComponent, MapInitEvent>(OnAlarmOnSpawn);
    }

    private void OnAnimationEnemyAcquired(Entity<CEGOAPAlarmAnimationComponent> ent, ref CEGOAPEnemyAcquiredEvent args)
    {
        if (args.FirstEnemy is null)
            return;

        if (_timing.CurTime > ent.Comp.LastAlarm + ent.Comp.Cooldown)
            _animation.TryPlayAnimationToEntity(ent, ent.Comp.Animation, args.FirstEnemy.Value, forceCancel: true);

        ent.Comp.LastAlarm = _timing.CurTime;

        Alarm(Transform(ent).Coordinates, args.FirstEnemy.Value, ent.Comp.Radius);
    }

    private void OnAlarmOnSpawn(Entity<CEAlarmOnSpawnComponent> ent, ref MapInitEvent args)
    {
        Alarm(Transform(ent).Coordinates, ent.Owner, ent.Comp.Radius);
    }

    private void OnEnemyAcquired(Entity<CEGOAPAlarmComponent> ent, ref CEGOAPEnemyAcquiredEvent args)
    {
        if (args.FirstEnemy is null)
            return;

        if (_timing.CurTime > ent.Comp.LastAlarm + ent.Comp.Cooldown)
        {
            var vfx = SpawnAttachedTo(ent.Comp.AlarmVFX, Transform(ent).Coordinates);
            _transform.SetParent(vfx, ent);
            _audio.PlayPvs(ent.Comp.Sound, ent);
        }

        ent.Comp.LastAlarm = _timing.CurTime;

        Alarm(Transform(ent).Coordinates, args.FirstEnemy.Value, ent.Comp.Radius);
    }

    private void Alarm(EntityCoordinates source, EntityUid target, float radius)
    {
        RaiseLocalEvent(new CEGOAPAlarmEvent(source, target, radius));
    }
}

/// <summary>
/// An event broadcast to alert GOAP agents within a radius
/// </summary>
public sealed partial class CEGOAPAlarmEvent(EntityCoordinates source, EntityUid target, float radius) : EntityEventArgs
{
    public EntityCoordinates Source = source;
    public EntityUid Target = target;
    public float Radius = radius;
}
