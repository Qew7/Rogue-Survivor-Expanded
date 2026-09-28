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
        protected ActorAction BehaviorNpcGroupPlans(RogueGame game, List<Actor> visible)
        {
            NpcGroupPlan plan = NpcStorySystem.ProposeGroupPlan(game, m_Actor, visible); if (plan == null) return null;
            Actor listener = NpcIntentSystem.VisibleTarget(visible, plan.CollectorId);
            if (listener == m_Actor || listener == null)
                listener = visible.Find(a => a.SocialGroup == m_Actor.SocialGroup && !a.IsSleeping);
            ActionNpcGroupPlan action = new ActionNpcGroupPlan(m_Actor, game, listener, plan);
            return action.IsLegal() ? action : null;
        }
        protected ActorAction BehaviorNpcRumors(RogueGame game, List<Actor> visible)
        {
            NpcKnowledge knowledge = m_Actor.Personality.Knowledge; int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            if (turn < knowledge.NextTalkTurn || 10 + PersonalitySystem.Bias(m_Actor, DecisionKind.Group) +
                PersonalitySystem.Bias(m_Actor, DecisionKind.Compassion) / 2 < 25) return null;
            foreach (NpcFact fact in knowledge.Facts)
            {
                if (fact.Kind == "missing_companion" || (fact.SubjectId == Guid.Empty && fact.Kind != "food_cache")) continue;
                foreach (Actor person in visible)
                {
                    if (person == m_Actor || person.PersonalityIdentity == fact.SourceId) continue;
                    RelationshipRecord opinion = m_Actor.Personality.Person(person.PersonalityIdentity);
                    if (opinion != null && (opinion.Trust < opinion.Fear || opinion.Feeling < -20)) continue;
                    var action = new ActionNpcTell(m_Actor, game, person, fact); if (action.IsLegal()) return action;
                }
            }
            return null;
        }
        protected ActorAction BehaviorNpcStoryIntent(RogueGame game, NpcIntent intent, List<Actor> visible)
        {
            NpcIntentMethod method = NpcIntentContent.Find(intent.DefinitionId).Method;
            Guid id = method == NpcIntentMethod.GatherFood && intent.Progress == 2 ? intent.CoordinatorId : intent.TargetId;
            Actor person = NpcIntentSystem.VisibleTarget(visible, id); ItemFood food = null;
            if (method == NpcIntentMethod.SeekPerson && person == null)
                foreach (Actor listener in visible)
                { var question = new ActionNpcAskLocation(m_Actor, game, listener, intent); if (question.IsLegal()) return question; }
            if (method == NpcIntentMethod.GatherFood && intent.Progress == 0 && intent.Destination.Map == m_Actor.Location.Map)
            {
                Inventory ground = m_Actor.Location.Map.GetItemsAt(intent.Destination.Position);
                // Inspect a stack only while the actor can actually see its remembered location.
                if (ground != null && NpcKnowledgeSystem.Visible(game, m_Actor, intent.Destination))
                    foreach (Item item in ground.Items) if (item is ItemFood && !game.Rules.IsFoodSpoiled((ItemFood)item, m_Actor.Location.Map.LocalTime.TurnCounter)) { food = (ItemFood)item; break; }
            }
            else if (person != null) food = NpcIntentSystem.SpareFood(game, m_Actor, person);
            var action = new ActionNpcStory(m_Actor, game, intent, person, food);
            if (action.IsLegal()) return action;
            if (method == NpcIntentMethod.AvoidPerson)
            {
                ActorAction retreat = null; int distance = game.Rules.GridDistance(m_Actor.Location.Position, intent.LastKnown.Position);
                foreach (Direction direction in Direction.COMPASS)
                {
                    Point position = (m_Actor.Location + direction).Position;
                    if (!m_Actor.Location.Map.IsInBounds(position.X, position.Y)) continue;
                    int next = game.Rules.GridDistance(position, intent.LastKnown.Position);
                    if (next <= distance) continue;
                    ActorAction move = game.Rules.IsBumpableFor(m_Actor, game, new Location(m_Actor.Location.Map, position));
                    if (move is ActionMoveStep && move.IsLegal()) { retreat = move; distance = next; }
                }
                if (retreat != null) return retreat;
            }
            Location destination = method == NpcIntentMethod.GatherFood ? intent.Progress == 0 ? intent.Destination :
                intent.Progress == 2 ? intent.CoordinatorPlace : intent.LastKnown : method == NpcIntentMethod.ReachShelter ? intent.Destination : intent.LastKnown;
            if (method == NpcIntentMethod.ReachShelter && destination.Map == m_Actor.Location.Map)
            {
                Location best = destination; int distance = Int32.MaxValue;
                for (int x = destination.Position.X - 1; x <= destination.Position.X + 1; x++)
                    for (int y = destination.Position.Y - 1; y <= destination.Position.Y + 1; y++)
                    {
                        Map map = destination.Map; if (!map.IsInBounds(x, y) || !map.GetTileAt(x, y).IsInside || map.GetActorAt(x, y) != null) continue;
                        Location place = new Location(map, new Point(x, y)); if (!NpcKnowledgeSystem.Visible(game, m_Actor, place)) continue;
                        int value = game.Rules.GridDistance(m_Actor.Location.Position, place.Position);
                        if (value < distance) { best = place; distance = value; }
                    }
                destination = best;
            }
            ActorAction travel = BehaviorNpcKnownRoute(game, destination);
            if (travel != null && travel.IsLegal()) return travel;
            NpcIntentSystem.Block(m_Actor, intent, "could not reach or interact at the reported location"); return null;
        }
        protected ActorAction BehaviorNpcKnownRoute(RogueGame game, Location destination)
        {
            if (destination.Map == null) return null;
            Map current = m_Actor.Location.Map;
            if (destination.Map == current) return BehaviorIntelligentBumpToward(game, destination.Position, false, false);
            var route = new Dictionary<Map, NpcKnownExit>(new NpcMapComparer()); var pending = new Queue<Map>();
            pending.Enqueue(current); route.Add(current, null);
            while (pending.Count > 0 && route.Count < 16)
            {
                Map map = pending.Dequeue();
                foreach (NpcKnownExit exit in m_Actor.Personality.Knowledge.Exits)
                {
                    if (exit.From.Map != map || route.ContainsKey(exit.To.Map)) continue;
                    route.Add(exit.To.Map, map == current ? exit : route[map]); pending.Enqueue(exit.To.Map);
                    if (route.Count >= 16) break;
                }
            }
            NpcKnownExit first;
            if (!route.TryGetValue(destination.Map, out first) || first == null) return null;
            if (m_Actor.Location == first.From)
            {
                Exit actual = current.GetExitAt(first.From.Position);
                if (actual == null || actual.ToMap != first.To.Map || actual.ToPosition != first.To.Position) return null;
                var use = new ActionUseExit(m_Actor, first.From.Position, game); return use.IsLegal() ? use : null;
            }
            return BehaviorIntelligentBumpToward(game, first.From.Position, false, false);
        }
    }
}
