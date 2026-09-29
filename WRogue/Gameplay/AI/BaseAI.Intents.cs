using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Gameplay.AI
{
    abstract partial class BaseAI
    {
        protected List<Actor> PrepareNpcIntents(RogueGame game, List<Percept> percepts)
        {
            if (!NpcIntentSystem.Enabled(m_Actor)) return null;
            NpcKnowledgeSystem.Perceive(game, m_Actor, percepts);
            List<Actor> visible = new List<Actor>();
            if (percepts != null) foreach (Percept percept in percepts)
            {
                Actor actor = percept.Percepted as Actor;
                if (actor != null && percept.Turn == m_Actor.Location.Map.LocalTime.TurnCounter &&
                    NpcIntentSystem.CanSee(game, m_Actor, actor)) visible.Add(actor);
            }
            bool danger = false;
            foreach (Actor actor in visible) if (game.Rules.AreEnemies(m_Actor, actor)) { danger = true; break; }
            NpcIntentSystem.Maintain(game, m_Actor, visible, danger, Order != null);
            return visible;
        }
        protected ActorAction BehaviorNpcEmergency(RogueGame game, List<Actor> visible)
        {
            NpcIntent intent = NpcIntentSystem.Select(m_Actor, catalog: game.NpcContent, dangerOnly: true);
            return intent == null ? null : BehaviorNpcPlan(game, intent, visible);
        }
        protected ActorAction BehaviorNpcDeparture(RogueGame game, List<Actor> visible)
        {
            NpcIntent intent = NpcIntentSystem.Select(m_Actor, true, game.NpcContent);
            if (intent == null || m_Actor.Leader == null) return null;
            return BehaviorNpcPlan(game, intent, visible);
        }
        protected ActorAction BehaviorNpcIntents(RogueGame game, List<Actor> visible)
        {
            if (!NpcIntentSystem.Enabled(m_Actor)) return null;
            if (visible == null) return null;
            foreach (NpcReaction reaction in m_Actor.Personality.Reactions)
            {
                Actor target = NpcIntentSystem.VisibleTarget(visible, reaction.TargetId);
                if (target == null) continue;
                ActionNpcReaction action = new ActionNpcReaction(m_Actor, game, reaction, target);
                if (action.IsLegal()) return action;
            }
            ActorAction groupPlan = BehaviorNpcGroupPlans(game, visible); if (groupPlan != null) return groupPlan;
            NpcIntent intent = NpcIntentSystem.Select(m_Actor, catalog: game.NpcContent); if (intent == null) return BehaviorNpcRumors(game, visible);
            return BehaviorNpcPlan(game, intent, visible);
        }
    }
}
