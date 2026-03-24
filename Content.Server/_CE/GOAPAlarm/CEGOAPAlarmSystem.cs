using Content.Server._CE.GOAP;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
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
    }
}
