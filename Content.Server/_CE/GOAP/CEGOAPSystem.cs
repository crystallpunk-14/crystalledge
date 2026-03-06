using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health.Components;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Main GOAP orchestrator system. Updates sensors, manages planning, and executes actions
/// for all entities with CEGOAPComponent.
/// </summary>
public sealed partial class CEGOAPSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _enabled = true;
    private int _maxUpdates = 128;
    private float _sensorInterval = 0.2f;

    /// <summary>
    /// Reusable list for executable actions to avoid allocations during planning.
    /// </summary>
    private readonly List<CEGOAPAction> _executableActions = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CEGOAPEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.CEGOAPMaxUpdates, v => _maxUpdates = v, true);
        Subs.CVar(_cfg, CCVars.CEGOAPSensorInterval, v => _sensorInterval = v, true);

        InitWake();

        SubscribeLocalEvent<CEGOAPComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEGOAPComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CEGOAPComponent> ent, ref MapInitEvent args)
    {
        UpdateAwakeStatus((ent, ent.Comp));
    }

    private void OnShutdown(Entity<CEGOAPComponent> ent, ref ComponentShutdown args)
    {
        ClearPlan(ent);
        RemCompDeferred<CEActiveGOAPComponent>(ent);
        RemCompDeferred<ActiveNPCComponent>(ent);
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

            UpdateAgent((uid, goap), frameTime);
            count++;
        }
    }

    private void UpdateAgent(Entity<CEGOAPComponent> ent, float frameTime)
    {
        // 1. Resolve target providers, then update sensors
        if (_timing.CurTime >= ent.Comp.NextSensorTime)
        {
            ResolveTargetProviders(ent);
            UpdateSensors(ent);
        }

        // 2. Check if we need to re-plan
        if (ent.Comp.CurrentPlan == null || _timing.CurTime >= ent.Comp.NextPlanTime)
            UpdatePlan(ent);

        // 3. Execute current action
        if (ent.Comp.CurrentPlan != null && ent.Comp.CurrentActionIndex < ent.Comp.CurrentPlan.Count)
            ExecuteCurrentAction(ent, frameTime);
    }

    private void ResolveTargetProviders(Entity<CEGOAPComponent> ent)
    {
        foreach (var (_, provider) in ent.Comp.TargetProviders)
        {
            provider.RaiseResolve(ent, EntityManager);
        }
    }

    private void UpdateSensors(Entity<CEGOAPComponent> ent)
    {
        ent.Comp.NextSensorTime = _timing.CurTime + TimeSpan.FromSeconds(_sensorInterval);
        foreach (var sensor in ent.Comp.Sensors)
        {
            sensor.RaiseUpdate(ent, ent.Comp.WorldState, EntityManager);
        }
    }

    private void UpdatePlan(Entity<CEGOAPComponent> ent)
    {
        ent.Comp.NextPlanTime = _timing.CurTime + ent.Comp.PlanCooldown;

        var bestGoalIndex = SelectBestGoal(ent.Comp);
        if (bestGoalIndex < 0)
        {
            ClearPlan(ent);
            return;
        }

        // If same goal and plan is still valid, keep it
        if (bestGoalIndex == ent.Comp.ActiveGoalIndex && ent.Comp.CurrentPlan != null)
            return;

        var bestGoal = ent.Comp.Goals[bestGoalIndex];

        // Filter actions by feasibility (CanExecute) before planning
        _executableActions.Clear();
        foreach (var action in ent.Comp.Actions)
        {
            if (action.RaiseCanExecute(ent, EntityManager))
                _executableActions.Add(action);
        }

        var plan = CEGOAPPlanner.Plan(ent.Comp.WorldState, bestGoal.DesiredState, _executableActions);

        if (plan != null && plan.Count > 0)
        {
            ShutdownCurrentAction(ent);
            ent.Comp.ActiveGoalIndex = bestGoalIndex;
            ent.Comp.CurrentPlan = plan;
            ent.Comp.CurrentActionIndex = 0;
            ent.Comp.CurrentActionStarted = false;
        }
        else
        {
            ClearPlan(ent);
        }
    }

    /// <summary>
    /// Return the index of the highest priority goal that has not yet been completed,
    /// for which the requirements have been met.
    /// </summary>
    private int SelectBestGoal(CEGOAPComponent goap)
    {
        var bestIndex = -1;
        var bestPriority = float.MinValue;

        for (var i = 0; i < goap.Goals.Count; i++)
        {
            var goal = goap.Goals[i];

            // We skip goals for which the requirements have not been met.
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

    private void ExecuteCurrentAction(Entity<CEGOAPComponent> ent, float frameTime)
    {
        var action = ent.Comp.CurrentPlan![ent.Comp.CurrentActionIndex];

        if (!ent.Comp.CurrentActionStarted)
        {
            action.RaiseStartup(ent, EntityManager);
            ent.Comp.CurrentActionStarted = true;
        }

        var status = action.RaiseUpdate(ent, frameTime, EntityManager);

        switch (status)
        {
            case CEGOAPActionStatus.Running:
                break;

            case CEGOAPActionStatus.Finished:
                action.RaiseShutdown(ent, EntityManager);
                ent.Comp.CurrentActionIndex++;
                ent.Comp.CurrentActionStarted = false;

                // Plan completed
                if (ent.Comp.CurrentActionIndex >= ent.Comp.CurrentPlan.Count)
                    ClearPlan(ent);
                break;

            case CEGOAPActionStatus.Failed:
                action.RaiseShutdown(ent, EntityManager);
                ClearPlan(ent);
                ent.Comp.NextPlanTime = TimeSpan.Zero; // Re-plan immediately
                break;
        }
    }

    private void ShutdownCurrentAction(Entity<CEGOAPComponent> ent)
    {
        if (ent.Comp.CurrentPlan != null &&
            ent.Comp.CurrentActionStarted &&
            ent.Comp.CurrentActionIndex < ent.Comp.CurrentPlan.Count)
        {
            ent.Comp.CurrentPlan[ent.Comp.CurrentActionIndex].RaiseShutdown(ent, EntityManager);
        }
    }

    private void ClearPlan(Entity<CEGOAPComponent> ent)
    {
        ShutdownCurrentAction(ent);
        ent.Comp.CurrentPlan = null;
        ent.Comp.CurrentActionIndex = 0;
        ent.Comp.CurrentActionStarted = false;
        ent.Comp.ActiveGoalIndex = -1;
    }
}
