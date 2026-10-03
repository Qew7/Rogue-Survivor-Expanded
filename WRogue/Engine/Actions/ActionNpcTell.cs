using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcTell : ActorAction
    {
        readonly Actor target;
        readonly NpcFact fact;
        public ActionNpcTell(Actor actor, RogueGame game, Actor target, NpcFact fact) : base(actor, game) { this.target = target; this.fact = fact; }
        public static bool CanAddress(RogueGame game, Actor speaker, Actor target, NpcFact fact)
        { return fact != null && target != null && !target.IsSleeping && target.Model.Abilities.IsIntelligent &&
            !target.Model.Abilities.IsUndead &&
            game.Rules.GridDistance(speaker.Location.Position, target.Location.Position) <= 4 &&
            !game.Rules.AreEnemies(speaker, target) && NpcConversation.EligibleListener(speaker, target, fact) &&
            NpcIntentSystem.CanSee(game, speaker, target); }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && fact != null &&
            m_Actor.Personality.Knowledge.Facts.Contains(fact) &&
            NpcConversation.EligibleFact(m_Game, m_Actor, fact, m_Actor.Location.Map.LocalTime.TurnCounter) &&
            CanAddress(m_Game, m_Actor, target, fact); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            NpcConversation.ShareRumor(m_Game, m_Actor, target, fact, false);
        }
    }
}
