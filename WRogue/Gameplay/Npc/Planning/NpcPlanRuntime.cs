using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcPlanRuntime
    {
        public static ActorAction Choose(RogueGame game, Actor owner, NpcIntent goal, IList<Actor> visible, Func<Location, ActorAction> route)
        {
            if (goal == null) return null;
            NpcContentCatalog catalog = game.NpcContent;
            NpcIntentDefinition capability = catalog.Capability(goal.DefinitionId);
            if (capability == null || capability.Result == null && capability.ResultFacts == null)
                return null;

            if (goal.Plan == null)
                goal.Plan = new NpcPlan {
                    DesiredState = goal.Generated != null ? goal.Generated.ResultState : capability.GetResult(catalog, null)
                };
            NpcPlan plan = goal.Plan;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            var domain = new NpcPlanDomain(game, owner, goal, visible, catalog);
            if (plan.Traits != domain.Traits || plan.CatalogFingerprint != catalog.Fingerprint)
            {
                plan.Invalidate();
                plan.NextPlanningTurn = turn;
                plan.CatalogFingerprint = catalog.Fingerprint;
                plan.DesiredState = capability.GetResult(catalog, goal.Generated);
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (domain.InitialState.Contains(plan.DesiredState))
                {
                    NpcIntentSystem.Finish(catalog, owner, goal, NpcIntentStatus.Completed,
                        "observed all desired effects");
                    return null;
                }
                NpcPlanStep step = plan.Current;
                if (!HasCurrentBinding(domain, step))
                {
                    if (turn < plan.NextPlanningTurn && plan.Revision == owner.Personality.Knowledge.Revision)
                        return null;
                    plan.Invalidate();
                    plan.Traits = domain.Traits;
                    plan.Revision = owner.Personality.Knowledge.Revision;
                    int expanded;
                    List<NpcPlanStep> steps = NpcGoalPlanner.Search(domain.InitialState, plan.DesiredState,
                        domain.Actions, out expanded);
                    plan.Expanded = expanded;
                    plan.NextPlanningTurn = turn + 8;
                    if (steps == null || steps.Count == 0)
                    {
                        NpcIntentSystem.Block(catalog, owner, goal,
                            "no plan from available knowledge and actions");
                        return null;
                    }
                    plan.Steps.AddRange(steps);
                    plan.Cursor = 0;
                    Session.Get.ResidentRecords.PlanChanged(owner, goal);
                    step = plan.Current;
                }
                NpcOperatorDefinition definition = catalog.Operator(step);
                var context = new NpcExecutionContext(game, owner, goal, step, visible, route);
                bool allowed = definition != null;
                if (definition != null)
                {
                    foreach (INpcActionGuard guard in catalog.ActionGuards)
                    {
                        NpcActionAccess access = guard.Check(context, definition);
                        if (access.Reaction != null) return access.Reaction;
                        if (!access.Allowed) { allowed = false; break; }
                    }
                }
                ActorAction action = allowed ? definition.Execute(context) : null;
                if (action != null && action.IsLegal()) return action;
                if (allowed && definition.Unavailable != null) definition.Unavailable(context);
                plan.Reject(step, turn);
                domain = new NpcPlanDomain(game, owner, goal, visible, catalog);
            }
            NpcIntentSystem.Block(catalog, owner, goal, "plan action is unavailable");
            return null;
        }

        static bool HasCurrentBinding(NpcPlanDomain domain, NpcPlanStep step)
        {
            // Keep the saved step instance: actions check it against the plan's current step.
            return step != null && domain.Actions.Exists(action =>
                action.OperatorId == step.OperatorId &&
                (action.OperatorId != null || action.Action == step.Action) &&
                action.Target == step.Target && action.Place == step.Place &&
                action.Applies(domain.InitialState));
        }
    }
}
