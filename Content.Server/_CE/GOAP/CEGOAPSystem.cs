using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Robust.Shared.Configuration;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Main GOAP orchestrator system. Updates sensors, manages planning, and executes actions
/// for all entities with CEGOAPComponent.
/// </summary>
public sealed partial class CEGOAPSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _enabled = true;
    private int _maxUpdates = 128;
    private float _sensorInterval = 0.2f;

    private EntityQuery<CEHealthComponent> _healthQuery;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CEGOAPEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.CEGOAPMaxUpdates, v => _maxUpdates = v, true);
        Subs.CVar(_cfg, CCVars.CEGOAPSensorInterval, v => _sensorInterval = v, true);

        _healthQuery = GetEntityQuery<CEHealthComponent>();

        SubscribeLocalEvent<CEGOAPComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEGOAPComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CEGOAPComponent> ent, ref MapInitEvent args)
    {
        WakeGOAP(ent);
    }

    private void OnShutdown(Entity<CEGOAPComponent> ent, ref ComponentShutdown args)
    {
        ClearPlan(ent, ent.Comp);
        RemCompDeferred<CEActiveGOAPComponent>(ent);
        RemCompDeferred<ActiveNPCComponent>(ent);
    }

    /// <summary>
    /// Activates GOAP processing for this entity.
    /// </summary>
    public void WakeGOAP(EntityUid uid, CEGOAPComponent? goap = null)
    {
        if (!Resolve(uid, ref goap, false))
            return;

        EnsureComp<CEActiveGOAPComponent>(uid);
        EnsureComp<ActiveNPCComponent>(uid);
    }

    /// <summary>
    /// Deactivates GOAP processing for this entity.
    /// </summary>
    public void SleepGOAP(EntityUid uid, CEGOAPComponent? goap = null)
    {
        if (!Resolve(uid, ref goap, false))
            return;

        ClearPlan(uid, goap);
        RemCompDeferred<CEActiveGOAPComponent>(uid);
        RemCompDeferred<ActiveNPCComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        var count = 0;
        var query = EntityQueryEnumerator<CEActiveGOAPComponent, CEGOAPComponent>();
        while (query.MoveNext(out var uid, out _, out var goap))
        {
            if (count >= _maxUpdates)
                break;

            if (!goap.Enabled)
                continue;

            // Skip dead or critical entities
            if (_healthQuery.TryComp(uid, out var health) &&
                health.CurrentState >= CEMobState.Critical)
            {
                SleepGOAP(uid, goap);
                continue;
            }

            UpdateAgent(uid, goap, frameTime);
            count++;
        }
    }

    private void UpdateAgent(EntityUid uid, CEGOAPComponent goap, float frameTime)
    {
        // 1. Update sensors with interval
        goap.SensorAccumulator += frameTime;
        if (goap.SensorAccumulator >= _sensorInterval)
        {
            goap.SensorAccumulator -= _sensorInterval;
            UpdateSensors(uid, goap);
        }

        // 2. Check if we need to re-plan
        goap.PlanAccumulator -= frameTime;

        if (goap.CurrentPlan == null || goap.PlanAccumulator <= 0)
        {
            goap.PlanAccumulator = goap.PlanCooldown;
            TryReplan(uid, goap);
        }

        // 3. Execute current action
        if (goap.CurrentPlan != null && goap.CurrentActionIndex < goap.CurrentPlan.Count)
        {
            ExecuteCurrentAction(uid, goap, frameTime);
        }
    }

    private void UpdateSensors(EntityUid uid, CEGOAPComponent goap)
    {
        foreach (var sensor in goap.Sensors)
        {
            sensor.RaiseUpdate(uid, goap.WorldState, EntityManager);
        }
    }

    private void TryReplan(EntityUid uid, CEGOAPComponent goap)
    {
        var bestGoalIndex = SelectBestGoal(goap);

        if (bestGoalIndex < 0)
        {
            ClearPlan(uid, goap);
            return;
        }

        // If same goal and plan is still valid, keep it
        if (bestGoalIndex == goap.ActiveGoalIndex && goap.CurrentPlan != null)
            return;

        var bestGoal = goap.Goals[bestGoalIndex];

        var plan = CEGOAPPlanner.Plan(goap.WorldState, bestGoal.DesiredState, goap.Actions);

        if (plan != null && plan.Count > 0)
        {
            ShutdownCurrentAction(uid, goap);
            goap.ActiveGoalIndex = bestGoalIndex;
            goap.CurrentPlan = plan;
            goap.CurrentActionIndex = 0;
            goap.CurrentActionStarted = false;
        }
        else
        {
            ClearPlan(uid, goap);
        }
    }

    private int SelectBestGoal(CEGOAPComponent goap)
    {
        var bestIndex = -1;
        var bestPriority = float.MinValue;

        for (var i = 0; i < goap.Goals.Count; i++)
        {
            var goal = goap.Goals[i];

            // Check activation conditions against current world state
            var active = true;
            foreach (var (key, value) in goal.ActivationConditions)
            {
                if (!goap.WorldState.TryGetValue(key, out var current) || current != value)
                {
                    active = false;
                    break;
                }
            }

            if (!active)
                continue;

            // Skip goals already satisfied
            var satisfied = true;
            foreach (var (key, value) in goal.DesiredState)
            {
                if (!goap.WorldState.TryGetValue(key, out var current) || current != value)
                {
                    satisfied = false;
                    break;
                }
            }

            if (satisfied)
                continue;

            if (goal.Priority > bestPriority)
            {
                bestIndex = i;
                bestPriority = goal.Priority;
            }
        }

        return bestIndex;
    }

    private void ExecuteCurrentAction(EntityUid uid, CEGOAPComponent goap, float frameTime)
    {
        var action = goap.CurrentPlan![goap.CurrentActionIndex];

        if (!goap.CurrentActionStarted)
        {
            action.RaiseStartup(uid, EntityManager);
            goap.CurrentActionStarted = true;
        }

        var status = action.RaiseUpdate(uid, frameTime, EntityManager);

        switch (status)
        {
            case CEGOAPActionStatus.Running:
                break;

            case CEGOAPActionStatus.Finished:
                action.RaiseShutdown(uid, EntityManager);
                goap.CurrentActionIndex++;
                goap.CurrentActionStarted = false;

                // Plan completed
                if (goap.CurrentActionIndex >= goap.CurrentPlan.Count)
                    ClearPlan(uid, goap);
                break;

            case CEGOAPActionStatus.Failed:
                action.RaiseShutdown(uid, EntityManager);
                ClearPlan(uid, goap);
                goap.PlanAccumulator = 0; // Re-plan immediately
                break;
        }
    }

    private void ShutdownCurrentAction(EntityUid uid, CEGOAPComponent goap)
    {
        if (goap.CurrentPlan != null &&
            goap.CurrentActionStarted &&
            goap.CurrentActionIndex < goap.CurrentPlan.Count)
        {
            goap.CurrentPlan[goap.CurrentActionIndex].RaiseShutdown(uid, EntityManager);
        }
    }

    private void ClearPlan(EntityUid uid, CEGOAPComponent goap)
    {
        ShutdownCurrentAction(uid, goap);
        goap.CurrentPlan = null;
        goap.CurrentActionIndex = 0;
        goap.CurrentActionStarted = false;
        goap.ActiveGoalIndex = -1;
    }
}
