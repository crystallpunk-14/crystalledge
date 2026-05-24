using Content.Server._CE.GOAP;
using Content.Server._CE.GOAP.Classifiers;
using Content.Shared._CE.Boss;
using Content.Shared._CE.Boss.Components;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.Health;
using Robust.Shared.Map;

namespace Content.Server._CE.Boss;

public sealed class CEBossSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBossComponent, CEGOAPEnemyAcquiredEvent>(OnEnemyAcquired);
        SubscribeLocalEvent<CEBossComponent, CEDestructedEvent>(OnDestructed);

        SubscribeLocalEvent<CEBossBattleStartedEvent>(OnStartFight);
        SubscribeLocalEvent<CEBossBattleEndedEvent>(OnEndFight);
    }

    private void OnStartFight(CEBossBattleStartedEvent ev)
    {
        // Start fights effects on that map
        var query = EntityQueryEnumerator<CEEffectOnBossFightStartComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var startEffect, out var xform))
        {
            if (xform.MapID != ev.MapId)
                continue;

            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                uid,
                null,
                Angle.Zero,
                1f,
                uid,
                Transform(uid).Coordinates);

            foreach (var effect in startEffect.Effects)
            {
                effect.Effect(effectArgs);
            }
        }
    }

    private void OnEndFight(CEBossBattleEndedEvent ev)
    {
        // End fights effects on that map
        var query = EntityQueryEnumerator<CEEffectOnBossFightEndComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var endEffect, out var xform))
        {
            if (xform.MapID != ev.MapId)
                continue;

            var effectArgs = new CEEntityEffectArgs(
                EntityManager,
                uid,
                null,
                Angle.Zero,
                1f,
                uid,
                Transform(uid).Coordinates);

            foreach (var effect in endEffect.Effects)
            {
                effect.Effect(effectArgs);
            }
        }
    }

    private void OnEnemyAcquired(Entity<CEBossComponent> ent, ref CEGOAPEnemyAcquiredEvent args)
    {
        if (ent.Comp.StartedFight)
            return;

        ent.Comp.StartedFight = true;
        Dirty(ent);

        if (TryComp<CEBossHealthBarComponent>(ent, out var healthBar))
        {
            healthBar.Active = true;
            Dirty(ent, healthBar);
        }

        RaiseLocalEvent(new CEBossBattleStartedEvent(Transform(ent).MapID));
    }

    private void OnDestructed(Entity<CEBossComponent> ent, ref CEDestructedEvent args)
    {
        var map = Transform(ent).MapID;

        var finishFight = true;
        var query = EntityQueryEnumerator<CEBossComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boss, out var xform))
        {
            if (uid == ent.Owner)
                continue;

            if (xform.MapID == map) //We still have another bosses on that map, so fight is not over
            {
                finishFight = false;
                break;
            }
        }

        if (finishFight)
            RaiseLocalEvent(new CEBossBattleEndedEvent(map));
    }
}

public sealed class CEBossBattleStartedEvent(MapId mapId) : EntityEventArgs
{
    public MapId MapId = mapId;
}

public sealed class CEBossBattleEndedEvent(MapId mapId) : EntityEventArgs
{
    public MapId MapId = mapId;
}
