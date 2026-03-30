using System.Linq;
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

    /// <summary>
    /// Note: CurrentPlan lists in entity components are reused and cleared/repopulated 
    /// rather than creating new lists each time to minimize GC allocations.
    /// </summary>

    /// <summary>
    /// Snapshot buffer for active GOAP entities. Populated at the start of each Update()
    /// to avoid collection-modified exceptions when WakeMob adds CEActiveGOAPComponent
    /// to new entities during action execution.
    /// </summary>
    private readonly List<(EntityUid Uid, CEGOAPComponent Goap)> _activeSnapshot = new();

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
        foreach (var action in ent.Comp.Actions)
        {
            foreach (var prec in action.Preconditions)
            {
                ent.Comp.WorldState[prec.Key] = false;
            }

            foreach (var effect in action.Effects)
            {
                ent.Comp.WorldState[effect.Key] = false;
            }
        }

        foreach (var goal in ent.Comp.Goals)
        {
            foreach (var state in goal.DesiredState)
            {
                ent.Comp.WorldState[state.Key] = false;
            }
            foreach (var prec in goal.Preconditions)
            {
                ent.Comp.WorldState[prec.Key] = false;
            }
        }

        // Force all sensors to evaluate once so WorldState is populated immediately.
        foreach (var sensor in ent.Comp.Sensors)
        {
            sensor.RaiseUpdate(ent, ent.Comp.WorldState, EntityManager);
        }

        // If StartSleeping is set, add the sleeping marker so the entity stays dormant.
        if (ent.Comp.StartSleeping)
            EnsureComp<CEGOAPSleepingComponent>(ent);

        UpdateAwakeStatus((ent, ent.Comp));
    }

    private void OnShutdown(Entity<CEGOAPComponent> ent, ref ComponentShutdown args)
    {
        CleanupTrackers(ent);
        ClearPlan(ent);
        RemCompDeferred<CEActiveGOAPComponent>(ent);
        RemCompDeferred<ActiveNPCComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled)
            return;

        // Snapshot active entities before iterating to prevent
        // InvalidOperationException if WakeMob adds CEActiveGOAPComponent
        // to a new entity during action execution.
        _activeSnapshot.Clear();
        var query = EntityQueryEnumerator<CEActiveGOAPComponent, CEGOAPComponent>();
        while (query.MoveNext(out var uid, out _, out var goap))
        {
            _activeSnapshot.Add((uid, goap));
        }

        var count = 0;
        foreach (var (uid, goap) in _activeSnapshot)
        {
            if (count >= _maxUpdates)
                break;

            if (!HasComp<CEActiveGOAPComponent>(uid))
                continue;

            UpdateAgent((uid, goap), frameTime);
            count++;
        }
    }

    private void UpdateAgent(Entity<CEGOAPComponent> ent, float frameTime)
    {
        // 1. Update sensors
        UpdateSensors(ent);

        // 2. Check if we need to re-plan
        if (ent.Comp.CurrentPlan.Count == 0 || _timing.CurTime >= ent.Comp.NextPlanTime)
            Replan(ent);

        // 3. Execute current action
        if (ent.Comp.CurrentPlan.Count != 0 && ent.Comp.CurrentActionIndex < ent.Comp.CurrentPlan.Count)
            ExecuteCurrentAction(ent, frameTime);
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

    private void Replan(Entity<CEGOAPComponent> ent)
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
            if (goalIndex == ent.Comp.ActiveGoalIndex && ent.Comp.CurrentPlan.Count > 0)
                return;

            var goal = ent.Comp.Goals[goalIndex];

            // Shutdown old action BEFORE clearing: plan list reuse means the old
            // action reference is lost once the list is cleared.
            ShutdownCurrentAction(ent);
            ent.Comp.CurrentActionStarted = false;
            ent.Comp.CurrentPlan.Clear();

            if (!CEGOAPPlanner.Plan(ent.Comp.WorldState, goal.DesiredState, _executableActions, ent.Comp.CurrentPlan))
                continue;

            if (ent.Comp.CurrentPlan.Count == 0)
                continue;

            ent.Comp.ActiveGoalIndex = goalIndex;
            ent.Comp.CurrentActionIndex = 0;
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
            foreach (var (key, value) in goal.Preconditions)
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
        if (ent.Comp.CurrentPlan is null)
            return;

        if (!ent.Comp.CurrentActionStarted)
            return;

        if (ent.Comp.CurrentActionIndex >= ent.Comp.CurrentPlan.Count)
            return;

        ent.Comp.CurrentPlan[ent.Comp.CurrentActionIndex].RaiseShutdown(ent, EntityManager);
    }

    private void ClearPlan(Entity<CEGOAPComponent> ent)
    {
        ShutdownCurrentAction(ent);
        ent.Comp.CurrentPlan.Clear();
        ent.Comp.CurrentActionIndex = 0;
        ent.Comp.CurrentActionStarted = false;
        ent.Comp.ActiveGoalIndex = -1;
    }
}
