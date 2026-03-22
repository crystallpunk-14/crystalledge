using Content.Shared._CE.GOAP;
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

    /// <summary>
    /// Reusable list for executable actions to avoid allocations during planning.
    /// </summary>
    private readonly List<CEGOAPAction> _executableActions = new();

    /// <summary>
    /// Reusable list for candidate goal indices, sorted by descending priority, to avoid per-frame allocations.
    /// </summary>
    private readonly List<int> _candidateGoals = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CEGOAPEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.CEGOAPMaxUpdates, v => _maxUpdates = v, true);

        InitWake();

        SubscribeLocalEvent<CEGOAPComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEGOAPComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CEGOAPComponent> ent, ref MapInitEvent args)
    {
        // Force all sensors to evaluate once so WorldState is populated immediately.
        ResolveTargetProviders(ent);

        foreach (var sensor in ent.Comp.Sensors)
        {
            sensor.RaiseUpdate(ent, ent.Comp.WorldState, EntityManager);
        }
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
        ResolveTargetProviders(ent);
        UpdateSensors(ent);

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
        var curTime = _timing.CurTime;

        foreach (var sensor in ent.Comp.Sensors)
        {
            var interval = sensor.UpdateInterval;

            // Event-only sensors are not polled.
            if (interval is null || interval.Value <= TimeSpan.Zero)
                continue;

            // Per-sensor timing.
            if (curTime < sensor.NextUpdateTime)
                continue;

            sensor.NextUpdateTime = curTime + interval.Value;
            sensor.RaiseUpdate(ent, ent.Comp.WorldState, EntityManager);
        }
    }

    private void UpdatePlan(Entity<CEGOAPComponent> ent)
    {
        ent.Comp.NextPlanTime = _timing.CurTime + ent.Comp.PlanCooldown;

        // Filter actions by feasibility (CanExecute) before planning — done once for all goals
        _executableActions.Clear();
        foreach (var action in ent.Comp.Actions)
        {
            if (action.RaiseCanExecute(ent, EntityManager))
                _executableActions.Add(action);
        }

        // Try active goals in descending priority order; adopt the first one that yields a valid plan
        GetActiveGoalIndicesByPriority(ent.Comp);
        foreach (var goalIndex in _candidateGoals)
        {
            // If this is already the active goal and plan is still valid, keep it
            if (goalIndex == ent.Comp.ActiveGoalIndex && ent.Comp.CurrentPlan != null)
                return;

            var goal = ent.Comp.Goals[goalIndex];
            var plan = CEGOAPPlanner.Plan(ent.Comp.WorldState, goal.DesiredState, _executableActions);

            if (plan == null || plan.Count == 0)
                continue;

            ShutdownCurrentAction(ent);
            ent.Comp.ActiveGoalIndex = goalIndex;
            ent.Comp.CurrentPlan = plan;
            ent.Comp.CurrentActionIndex = 0;
            ent.Comp.CurrentActionStarted = false;
            return;
        }

        // No goal could be planned with the currently feasible actions
        ClearPlan(ent);
    }

    /// <summary>
    /// Fills <see cref="_candidateGoals"/> with indices of goals that are active (activation conditions met)
    /// and not yet satisfied, sorted by descending priority.
    /// </summary>
    private void GetActiveGoalIndicesByPriority(CEGOAPComponent goap)
    {
        _candidateGoals.Clear();

        for (var i = 0; i < goap.Goals.Count; i++)
        {
            var goal = goap.Goals[i];

            // Skip goals whose activation conditions are not currently met
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

            // Skip goals that are already satisfied
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

            _candidateGoals.Add(i);
        }

        _candidateGoals.Sort((a, b) => goap.Goals[b].Priority.CompareTo(goap.Goals[a].Priority));
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
