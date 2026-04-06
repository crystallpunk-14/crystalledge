using System.Numerics;
using Content.Server._CE.GOAPAlarm;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
using Content.Shared._CE.Procedural.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Manages sleeping GOAP entities. Wakes them on:
/// - Player proximity (iterates players, not mobs, for performance)
/// - Damage received
/// - Alarm events (noise/explosions)
/// - Chain reaction from a nearby mob waking
/// </summary>
public sealed class CEGOAPSleepingSystem : EntitySystem
{
    [Dependency] private readonly CEGOAPSystem _goap = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// How often proximity checks run.
    /// </summary>
    private static readonly TimeSpan ProximityCheckInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextProximityCheck;

    private readonly HashSet<Entity<CEGOAPSleepingComponent>> _nearbyBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        // Wake on damage
        SubscribeLocalEvent<CEGOAPSleepingComponent, CEDamageChangedEvent>(OnDamageChanged);

        // Wake on alarm (noise, combat sounds nearby)
        SubscribeLocalEvent<CEGOAPAlarmEvent>(OnAlarm);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextProximityCheck)
            return;

        _nextProximityCheck = _timing.CurTime + ProximityCheckInterval;

        var playerQuery = EntityQueryEnumerator<ActorComponent, CEDungeonPlayerComponent, TransformComponent>();
        while (playerQuery.MoveNext(out _, out _, out _, out var xform))
        {
            if (xform.MapUid is null)
                continue;

            _nearbyBuffer.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, 8f, _nearbyBuffer);

            foreach (var sleeping in _nearbyBuffer)
            {
                var mobPos = _transform.GetWorldPosition(sleeping);
                var playerPos = _transform.GetWorldPosition(xform);
                var distance = Vector2.Distance(mobPos, playerPos);

                if (distance <= sleeping.Comp.WakeRadius)
                    WakeMob(sleeping);
            }
        }
    }

    private void OnDamageChanged(Entity<CEGOAPSleepingComponent> ent, ref CEDamageChangedEvent args)
    {
        if (args.DamageDelta <= 0)
            return;

        WakeMob(ent);
    }

    private void OnAlarm(CEGOAPAlarmEvent ev)
    {
        _nearbyBuffer.Clear();
        _lookup.GetEntitiesInRange(ev.Source, ev.Radius, _nearbyBuffer);

        foreach (var sleeping in _nearbyBuffer)
        {
            WakeMob(sleeping);
        }
    }

    /// <summary>
    /// Wakes a sleeping mob: removes the sleeping marker, re-evaluates GOAP awake status,
    /// and chain-wakes nearby sleeping mobs.
    /// </summary>
    public void WakeMob(Entity<CEGOAPSleepingComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // Must use RemComp (not Deferred) so HasComp check in OnCheckAwake
        // sees the component as absent when UpdateAwakeStatus runs immediately after.
        RemComp<CEGOAPSleepingComponent>(ent);

        // Re-evaluate GOAP awake status — with the sleeping component removed,
        // the normal wake check in CEGOAPSystem will now succeed.
        if (TryComp<CEGOAPComponent>(ent, out var goap))
            _goap.UpdateAwakeStatus((ent, goap));
    }
}
