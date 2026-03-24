using System.Numerics;
using Content.Shared._CE.GOAP;

namespace Content.Server._CE.GOAP;

/// <summary>
/// GOAP planner using forward A* search with bitmask-packed state.
/// String keys are mapped to bit indices once per call, so the search
/// operates entirely on integers — zero heap allocations during A*.
/// </summary>
public static class CEGOAPPlanner
{
    private struct PlanNode
    {
        public int State;
        public int ActionIndex;  // -1 for start node
        public int ParentIndex;  // -1 for start node
        public float GCost;
        public float HCost;
    }

    private struct CompiledAction
    {
        public int PrecMask;
        public int PrecRequired;
        public int EffMask;
        public int EffRequired;
        public float Cost;
    }

    // Reusable structures cleared between calls.
    // Safe because the game loop is single-threaded.
    private static readonly Dictionary<string, int> KeyMap = new();
    private static readonly List<CompiledAction> CompiledActions = new();
    private static readonly List<PlanNode> Nodes = new();
    private static readonly PriorityQueue<int, float> OpenList = new();
    private static readonly HashSet<int> ClosedStates = new();

    /// <summary>
    /// Plans a sequence of actions to achieve the goal from the current state.
    /// Returns true if a plan was found and populates the output plan list.
    /// </summary>
    public static bool Plan(
        Dictionary<string, bool> currentState,
        Dictionary<string, bool> goalState,
        List<CEGOAPAction> availableActions,
        List<CEGOAPAction> outPlan,
        int maxIterations = 100)
    {
        KeyMap.Clear();
        CompiledActions.Clear();
        Nodes.Clear();
        OpenList.Clear();
        ClosedStates.Clear();

        BuildKeyMap(currentState, goalState, availableActions);

        var startBits = ToBitmask(currentState);
        ToBitmaskCondition(goalState, out var goalMask, out var goalRequired);

        for (var i = 0; i < availableActions.Count; i++)
        {
            var action = availableActions[i];
            ToBitmaskCondition(action.Preconditions, out var precMask, out var precReq);
            ToBitmaskCondition(action.Effects, out var effMask, out var effReq);
            CompiledActions.Add(new CompiledAction
            {
                PrecMask = precMask,
                PrecRequired = precReq,
                EffMask = effMask,
                EffRequired = effReq,
                Cost = action.Cost,
            });
        }

        var hStart = Heuristic(startBits, goalMask, goalRequired);
        var startIdx = AddNode(startBits, -1, -1, 0f, hStart);
        OpenList.Enqueue(startIdx, hStart);

        var iterations = 0;
        while (OpenList.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            var currentIdx = OpenList.Dequeue();
            var current = Nodes[currentIdx];

            if ((current.State & goalMask) == goalRequired)
            {
                ReconstructPlan(currentIdx, availableActions, outPlan);
                return true;
            }

            if (!ClosedStates.Add(current.State))
                continue;

            for (var i = 0; i < CompiledActions.Count; i++)
            {
                var compiled = CompiledActions[i];

                if ((current.State & compiled.PrecMask) != compiled.PrecRequired)
                    continue;

                var newState = (current.State & ~compiled.EffMask) | compiled.EffRequired;

                if (ClosedStates.Contains(newState))
                    continue;

                var gCost = current.GCost + compiled.Cost;
                var hCost = Heuristic(newState, goalMask, goalRequired);
                var newIdx = AddNode(newState, i, currentIdx, gCost, hCost);
                OpenList.Enqueue(newIdx, gCost + hCost);
            }
        }

        return false;
    }

    private static int AddNode(int state, int actionIndex, int parentIndex, float gCost, float hCost)
    {
        var idx = Nodes.Count;
        Nodes.Add(new PlanNode
        {
            State = state,
            ActionIndex = actionIndex,
            ParentIndex = parentIndex,
            GCost = gCost,
            HCost = hCost,
        });
        return idx;
    }

    private static void BuildKeyMap(
        Dictionary<string, bool> currentState,
        Dictionary<string, bool> goalState,
        List<CEGOAPAction> actions)
    {
        foreach (var key in currentState.Keys)
            TryAddKey(key);

        foreach (var key in goalState.Keys)
            TryAddKey(key);

        foreach (var action in actions)
        {
            foreach (var key in action.Preconditions.Keys)
                TryAddKey(key);

            foreach (var key in action.Effects.Keys)
                TryAddKey(key);
        }
    }

    private static void TryAddKey(string key)
    {
        if (!KeyMap.ContainsKey(key))
            KeyMap[key] = KeyMap.Count;
    }

    private static int ToBitmask(Dictionary<string, bool> state)
    {
        var bits = 0;
        foreach (var (key, value) in state)
        {
            if (value && KeyMap.TryGetValue(key, out var index))
                bits |= 1 << index;
        }

        return bits;
    }

    private static void ToBitmaskCondition(
        Dictionary<string, bool> conditions,
        out int mask,
        out int required)
    {
        mask = 0;
        required = 0;
        foreach (var (key, value) in conditions)
        {
            if (!KeyMap.TryGetValue(key, out var index))
                continue;

            mask |= 1 << index;
            if (value)
                required |= 1 << index;
        }
    }

    private static float Heuristic(int state, int goalMask, int goalRequired)
    {
        var diff = (state ^ goalRequired) & goalMask;
        return BitOperations.PopCount((uint) diff);
    }

    private static void ReconstructPlan(
        int goalNodeIndex,
        List<CEGOAPAction> availableActions,
        List<CEGOAPAction> outPlan)
    {
        outPlan.Clear();

        var idx = goalNodeIndex;
        while (idx >= 0)
        {
            var node = Nodes[idx];
            if (node.ActionIndex >= 0)
                outPlan.Add(availableActions[node.ActionIndex]);
            idx = node.ParentIndex;
        }

        outPlan.Reverse();
    }
}
