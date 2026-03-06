using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Main GOAP orchestrator system. Updates sensors, manages planning, and executes actions
/// for all entities with CEGOAPComponent.
/// </summary>
public sealed partial class CEGOAPSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<CEGOAPComponent> ent, ref ComponentShutdown args)
    {
        ClearPlan(ent, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEGOAPComponent>();
        while (query.MoveNext(out var uid, out var goap))
        {
            if (!goap.Enabled)
                continue;

            UpdateAgent(uid, goap, frameTime);
        }
    }

    private void UpdateAgent(EntityUid uid, CEGOAPComponent goap, float frameTime)
    {
        // 1. Update sensors to get current world state
        UpdateSensors(uid, goap);

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
        var bestGoal = SelectBestGoal(goap);

        if (bestGoal == null)
        {
            ClearPlan(uid, goap);
            return;
        }

        // If same goal and plan is still valid, keep it
        if (bestGoal == goap.ActiveGoal && goap.CurrentPlan != null)
            return;

        // Build goal state dictionary with string keys
        var goalState = new Dictionary<string, bool>();
        foreach (var (key, value) in bestGoal.DesiredState)
        {
            goalState[(string) key] = value;
        }

        var plan = CEGOAPPlanner.Plan(goap.WorldState, goalState, goap.Actions);

        if (plan != null && plan.Count > 0)
        {
            ShutdownCurrentAction(uid, goap);
            goap.ActiveGoal = bestGoal;
            goap.CurrentPlan = plan;
            goap.CurrentActionIndex = 0;
            goap.CurrentActionStarted = false;
        }
        else
        {
            ClearPlan(uid, goap);
        }
    }

    private CEGOAPGoal? SelectBestGoal(CEGOAPComponent goap)
    {
        CEGOAPGoal? best = null;
        var bestPriority = float.MinValue;

        foreach (var goal in goap.Goals)
        {
            // Check activation conditions against current world state
            var active = true;
            foreach (var (key, value) in goal.ActivationConditions)
            {
                if (!goap.WorldState.TryGetValue((string) key, out var current) || current != value)
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
                if (!goap.WorldState.TryGetValue((string) key, out var current) || current != value)
                {
                    satisfied = false;
                    break;
                }
            }

            if (satisfied)
                continue;

            if (goal.Priority > bestPriority)
            {
                best = goal;
                bestPriority = goal.Priority;
            }
        }

        return best;
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
        goap.ActiveGoal = null;
    }
}
