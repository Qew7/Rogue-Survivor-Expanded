using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionPlayerTalk : ActorAction
    {
        readonly Actor target;
        readonly bool? answer;
        public ActionPlayerTalk(Actor player, RogueGame game, Actor target, bool? answer) : base(player, game)
        { this.target = target; this.answer = answer; }
        public override bool IsLegal()
        {
            if (!m_Actor.IsPlayer || target == null || target.IsDead || m_Actor.IsSleeping || target.IsSleeping ||
                !target.Model.Abilities.IsIntelligent || target.Model.Abilities.IsUndead ||
                m_Actor.Location.Map != target.Location.Map ||
                m_Game.Rules.GridDistance(m_Actor.Location.Position, target.Location.Position) > 1 ||
                m_Game.Rules.AreEnemies(m_Actor, target)) return false;
            NpcReaction pending = NpcConversation.Pending(m_Actor, target);
            if (pending == null) return true;
            if (!answer.HasValue) return false;
            NpcEventDefinition request = m_Game.NpcContent.Event(pending.Kind);
            if (request == null || request.PlayerReply == null) return false;
            if (!answer.Value) return true;
            NpcEventDefinition outcome = m_Game.NpcContent.Event(request.PlayerReply.YesKind);
            return outcome == null || outcome.CanReply == null || outcome.CanReply(m_Actor, target);
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            NpcReaction pending = NpcConversation.Pending(m_Actor, target);
            if (pending != null) { NpcConversation.Reply(m_Game, m_Actor, target, pending, answer.Value); return; }
            m_Game.DoSay(m_Actor, target, "Any news?", RogueGame.Sayflags.NONE);
            NpcFact rumor = NpcConversation.Rumor(m_Game, target, m_Actor);
            if (rumor != null) NpcConversation.ShareRumor(m_Game, target, m_Actor, rumor, true);
            else m_Game.DoSay(target, m_Actor, "Nothing new for now.", RogueGame.Sayflags.IS_FREE_ACTION);
        }
    }
}
