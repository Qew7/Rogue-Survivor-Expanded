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
            if (!NpcIntentSystem.Enabled(m_Actor) || (!m_Actor.Personality.HasPendingSocialState &&
                !game.Rules.IsActorHungry(m_Actor))) return null;
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
        protected ActorAction BehaviorNpcDeparture(RogueGame game)
        {
            NpcIntent intent = NpcIntentSystem.Select(m_Actor, true);
            if (intent == null || m_Actor.Leader == null) return null;
            ActionNpcIntent action = new ActionNpcIntent(m_Actor, game, intent, m_Actor.Leader);
            return action.IsLegal() ? action : null;
        }
        protected ActorAction BehaviorNpcIntents(RogueGame game, List<Actor> visible)
        {
            if (!NpcIntentSystem.Enabled(m_Actor)) return null;
            if (visible == null) return null;
            NpcIntentSystem.ConsiderFoodRequest(game, m_Actor, visible);
            foreach (NpcReaction reaction in m_Actor.Personality.Reactions)
            {
                Actor target = NpcIntentSystem.VisibleTarget(visible, reaction.TargetId);
                if (target == null) continue;
                ActionNpcReaction action = new ActionNpcReaction(m_Actor, game, reaction, target);
                if (action.IsLegal()) return action;
            }
            NpcIntent intent = NpcIntentSystem.Select(m_Actor); if (intent == null) return null;
            Actor person = NpcIntentSystem.VisibleTarget(visible, intent.TargetId);
            if (person != null)
            {
                ActionNpcIntent action = new ActionNpcIntent(m_Actor, game, intent, person,
                    NpcIntentSystem.SpareFood(game, m_Actor, person));
                if (action.IsLegal()) return action;
                if (game.Rules.GridDistance(m_Actor.Location.Position, person.Location.Position) <= 1)
                { NpcIntentSystem.Block(m_Actor, intent, "could not perform the intended interaction"); return null; }
            }
            if (intent.LastKnown.Map != m_Actor.Location.Map)
            { NpcIntentSystem.Block(m_Actor, intent, "last known location is on another map"); return null; }
            ActorAction move = BehaviorIntelligentBumpToward(game, intent.LastKnown.Position, false, false);
            if (move != null && move.IsLegal()) return move;
            NpcIntentSystem.Block(m_Actor, intent, "target could not be reached at its last known location"); return null;
        }
    }
}
