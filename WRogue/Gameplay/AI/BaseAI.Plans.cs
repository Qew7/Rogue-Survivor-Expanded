using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        protected ActorAction BehaviorNpcPlan(RogueGame game, NpcIntent goal, List<Actor> visible)
        {
            if (goal.Plan == null) goal.Plan = new NpcPlan { Desired = goal.Generated != null ? goal.Generated.Result : NpcGoalPlanner.Desired(NpcIntentContent.Find(goal.DefinitionId).Method) };
            NpcPlan plan = goal.Plan; int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            var domain = new NpcPlanDomain(game, m_Actor, goal, visible);
            if (plan.Traits != domain.Traits) { plan.Invalidate(); plan.NextPlanningTurn = turn; }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                NpcPlanStep step = plan.Current;
                NpcPlanStep binding = step == null ? null : domain.Actions.Find(a => a.Action == step.Action && a.Target == step.Target && a.Place == step.Place && a.Applies(domain.Initial));
                if (binding == null || !binding.Applies(domain.Initial))
                {
                    if (turn < plan.NextPlanningTurn && plan.Revision == m_Actor.Personality.Knowledge.Revision) return null;
                    plan.Invalidate(); plan.Traits = domain.Traits; plan.Revision = m_Actor.Personality.Knowledge.Revision;
                    int expanded; List<NpcPlanStep> steps = NpcGoalPlanner.Search(domain.Initial, plan.Desired, domain.Actions, out expanded);
                    plan.Expanded = expanded; plan.NextPlanningTurn = turn + 8;
                    if (steps == null || steps.Count == 0) { NpcIntentSystem.Block(m_Actor, goal, "no plan from available knowledge and actions"); return null; }
                    plan.Steps.AddRange(steps); plan.Cursor = 0;
                    Session.Get.ResidentRecords.PlanChanged(m_Actor, goal);
                    step = plan.Current;
                }
                Actor person = NpcIntentSystem.VisibleTarget(visible, step.Target);
                ActorAction movement = null; ItemFood food = null;
                if (step.Action == NpcPlanAction.Travel)
                {
                    Location place = step.Place;
                    if (NpcIntentContent.Find(goal.DefinitionId).Method == NpcIntentMethod.ReachShelter && place.Map == m_Actor.Location.Map)
                    {
                        int best = Int32.MaxValue;
                        for (int x = step.Place.Position.X - 1; x <= step.Place.Position.X + 1; x++)
                            for (int y = step.Place.Position.Y - 1; y <= step.Place.Position.Y + 1; y++)
                            {
                                Map map = place.Map;
                                if (!map.IsInBounds(x, y) || !map.GetTileAt(x, y).IsInside || map.GetActorAt(x, y) != null) continue;
                                Location candidate = new Location(map, new Point(x, y)); if (!NpcKnowledgeSystem.Visible(game, m_Actor, candidate)) continue;
                                int d = game.Rules.GridDistance(m_Actor.Location.Position, candidate.Position);
                                if (d < best) { place = candidate; best = d; }
                            }
                    }
                    movement = BehaviorNpcKnownRoute(game, place);
                }
                else if (step.Action == NpcPlanAction.Retreat)
                {
                    int distance = game.Rules.GridDistance(m_Actor.Location.Position, goal.LastKnown.Position);
                    foreach (Direction direction in Direction.COMPASS)
                    {
                        Location place = m_Actor.Location + direction;
                        if (!place.Map.IsInBounds(place.Position.X, place.Position.Y)) continue;
                        int d = game.Rules.GridDistance(place.Position, goal.LastKnown.Position);
                        if (d <= distance) continue;
                        ActorAction move = game.Rules.IsBumpableFor(m_Actor, game, place);
                        if (move is ActionMoveStep && move.IsLegal()) { movement = move; distance = d; }
                    }
                }
                else if (step.Action == NpcPlanAction.PickupFood)
                    food = NpcPlanExecution.FoodOnGround(game, m_Actor, step.Place);
                else if (step.Action == NpcPlanAction.GiveFood && person != null) food = NpcIntentSystem.SpareFood(game, m_Actor, person);
                if (step.Action == NpcPlanAction.PickupMedicine || step.Action == NpcPlanAction.UseMedicine)
                {
                    var medicine = new ActionNpcMedicine(m_Actor, game, goal, step);
                    if (medicine.IsLegal() && (step.Action != NpcPlanAction.PickupMedicine || Session.Get.NpcDirector.Reserve(goal.StoryId, step.Place))) return medicine;
                    if (step.Action == NpcPlanAction.PickupMedicine && NpcKnowledgeSystem.Visible(game, m_Actor, step.Place) &&
                        NpcPlanExecution.Medicine(game, m_Actor, step.Place) == null)
                        m_Actor.Personality.Knowledge.RememberPlace(new NpcKnownPlace(step.Place, "medicine", turn));
                }
                var action = new ActionNpcPlan(m_Actor, game, goal, step, person, food, movement);
                if (step.Action == NpcPlanAction.BarterFood)
                {
                    var barter = new ActionNpcBarter(m_Actor, game, goal, step, person);
                    if (barter.IsLegal()) return barter;
                }
                if ((step.Action != NpcPlanAction.Travel && step.Action != NpcPlanAction.Retreat || movement != null) && action.IsLegal())
                {
                    if (step.Action == NpcPlanAction.PickupFood && !Session.Get.NpcDirector.Reserve(goal.StoryId, step.Place))
                    { plan.Reject(step, turn); domain = new NpcPlanDomain(game, m_Actor, goal, visible); continue; }
                    return action;
                }
                if (step.Action == NpcPlanAction.PickupFood && NpcKnowledgeSystem.Visible(game, m_Actor, step.Place) && food == null)
                    m_Actor.Personality.Knowledge.RememberPlace(new NpcKnownPlace(step.Place, "food", turn));
                plan.Reject(step, turn); domain = new NpcPlanDomain(game, m_Actor, goal, visible);
            }
            NpcIntentSystem.Block(m_Actor, goal, "plan action is unavailable"); return null;
        }
    }
}
