using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// GOAP planner using forward A* search from current state to goal state.
/// Finds the cheapest sequence of actions to achieve a desired world state.
/// </summary>
public static class CEGOAPPlanner
{
    private sealed class PlanNode(
        Dictionary<string, bool> state,
        CEGOAPAction? action,
        PlanNode? parent,
        float gCost,
        float hCost)
    {
        public readonly Dictionary<string, bool> State = state;
        public readonly CEGOAPAction? Action = action;
        public readonly PlanNode? Parent = parent;
        public readonly float GCost = gCost;
        public readonly float HCost = hCost;
        public float FCost => GCost + HCost;
    }

    /// <summary>
    /// Plans a sequence of actions to achieve the goal from the current state.
    /// Returns null if no plan is found.
    /// </summary>
    public static List<CEGOAPAction>? Plan(
        Dictionary<string, bool> currentState,
        Dictionary<string, bool> goalState,
        List<CEGOAPAction> availableActions,
        int maxIterations = 100)
    {
        var startNode = new PlanNode(
            new Dictionary<string, bool>(currentState),
            null,
            null,
            0f,
            Heuristic(currentState, goalState));

        var openList = new PriorityQueue<PlanNode, float>();
        openList.Enqueue(startNode, startNode.FCost);

        var closedStates = new HashSet<int>();
        var iterations = 0;

        while (openList.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            var current = openList.Dequeue();

            if (GoalSatisfied(current.State, goalState))
                return ReconstructPlan(current);

            var stateHash = GetStateHash(current.State);
            if (!closedStates.Add(stateHash))
                continue;

            foreach (var action in availableActions)
            {
                if (!PreconditionsMet(current.State, action.Preconditions))
                    continue;

                var newState = ApplyEffects(current.State, action.Effects);
                var newStateHash = GetStateHash(newState);

                if (closedStates.Contains(newStateHash))
                    continue;

                var gCost = current.GCost + action.Cost;
                var hCost = Heuristic(newState, goalState);
                var newNode = new PlanNode(newState, action, current, gCost, hCost);
                openList.Enqueue(newNode, newNode.FCost);
            }
        }

        return null;
    }

    private static bool GoalSatisfied(
        Dictionary<string, bool> state,
        Dictionary<string, bool> goal)
    {
        foreach (var (key, value) in goal)
        {
            if (!state.TryGetValue(key, out var current) || current != value)
                return false;
        }

        return true;
    }

    private static bool PreconditionsMet(
        Dictionary<string, bool> state,
        Dictionary<string, bool> preconditions)
    {
        foreach (var (key, value) in preconditions)
        {
            if (!state.TryGetValue(key, out var current) || current != value)
                return false;
        }

        return true;
    }

    private static Dictionary<string, bool> ApplyEffects(
        Dictionary<string, bool> state,
        Dictionary<string, bool> effects)
    {
        var newState = new Dictionary<string, bool>(state);
        foreach (var (key, value) in effects)
        {
            newState[key] = value;
        }

        return newState;
    }

    private static float Heuristic(
        Dictionary<string, bool> state,
        Dictionary<string, bool> goal)
    {
        var unsatisfied = 0;
        foreach (var (key, value) in goal)
        {
            if (!state.TryGetValue(key, out var current) || current != value)
                unsatisfied++;
        }

        return unsatisfied;
    }

    private static int GetStateHash(Dictionary<string, bool> state)
    {
        var hash = new HashCode();
        var keys = new List<string>(state.Keys);
        keys.Sort(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            hash.Add(key);
            hash.Add(state[key]);
        }

        return hash.ToHashCode();
    }

    private static List<CEGOAPAction> ReconstructPlan(PlanNode goalNode)
    {
        var plan = new List<CEGOAPAction>();
        var current = goalNode;

        while (current?.Action != null)
        {
            plan.Add(current.Action);
            current = current.Parent;
        }

        plan.Reverse();
        return plan;
    }
}
