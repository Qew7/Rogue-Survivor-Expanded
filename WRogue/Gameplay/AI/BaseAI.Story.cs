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
                if (fact.Kind == "missing_companion" || (fact.SubjectId == Guid.Empty && fact.Kind != "food_cache" && fact.Kind != "medicine_cache")) continue;
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
