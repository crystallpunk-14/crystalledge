
using Robust.Shared.Timing;

namespace Content.Shared._CE.Animation.Effects;

public sealed partial class CEAnimationActionsSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEAnimationActionsSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEAnimationActionsSpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextEffectTime = _timing.CurTime + ent.Comp.FirstDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEAnimationActionsSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (_timing.CurTime < spawner.NextEffectTime)
                continue;

            spawner.NextEffectTime = _timing.CurTime + spawner.Frequency;

            var pos = Transform(uid).Coordinates;

            foreach (var effect in spawner.Effects)
            {
                effect.Play(EntityManager, uid, null, Angle.Zero, 1f, TimeSpan.Zero, uid, pos);
            }
        }
    }
}
