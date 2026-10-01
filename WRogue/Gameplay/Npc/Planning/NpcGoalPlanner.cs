using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Bounded uniform-cost search. Definitions describe actions, never complete plots.
    static class NpcGoalPlanner
    {
        sealed class Node
        {
            public NpcPlanningState State;
            public int Cost, Depth;
            public Node Parent;
            public NpcPlanStep Step;
        }
        public static List<NpcPlanStep> Search(ulong initial, ulong desired, IList<NpcPlanStep> actions, out int expanded)
        {
            return Search((NpcPlanningState)initial, (NpcPlanningState)desired, actions, out expanded);
        }
        public static List<NpcPlanStep> Search(NpcPlanningState initial, NpcPlanningState desired, IList<NpcPlanStep> actions, out int expanded)
        {
            expanded = 0;
            if (desired.Empty) return null;
            var open = new List<Node> { new Node { State = initial } };
            var costs = new Dictionary<NpcPlanningState, int> { { initial, 0 } };
            while (open.Count > 0 && expanded < 128)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].Cost < open[best].Cost) best = i;
                Node node = open[best]; open.RemoveAt(best);
                if (costs[node.State] < node.Cost) continue;
                expanded++;
                if (node.State.Contains(desired))
                {
                    var result = new List<NpcPlanStep>();
                    for (Node current = node; current.Step != null; current = current.Parent) result.Add(current.Step);
                    result.Reverse(); return result;
                }
                if (node.Depth >= 10) continue;
                foreach (NpcPlanStep action in actions)
                {
                    if (!action.Applies(node.State)) continue;
                    NpcPlanningState next = action.Predict(node.State); int cost = node.Cost + Math.Max(1, action.Cost), old;
                    if (costs.TryGetValue(next, out old) && old <= cost) continue;
                    if (open.Count >= 512) continue;
                    costs[next] = cost;
                    open.Add(new Node { State = next, Cost = cost, Depth = node.Depth + 1, Parent = node, Step = action });
                }
            }
            return null;
        }
    }
}
