using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcMedicineTrade : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        readonly Actor seller;
        Item payment;
        ItemMedicine medicine;
        public ActionNpcMedicineTrade(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step, Actor seller) : base(actor, game)
        { this.goal = goal; this.step = step; this.seller = seller; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step || step.Action != NpcPlanAction.BarterMedicine ||
                seller == null || seller.PersonalityIdentity != step.Target || seller.IsPlayer || seller.IsSleeping || seller == m_Actor ||
                !NpcIntentSystem.CanSee(m_Game, m_Actor, seller) || !NpcPlanExecution.Near(m_Game, m_Actor, seller.Location) ||
                !m_Game.Rules.CanActorInitiateTradeWith(m_Actor, seller) || seller.HitPoints < m_Game.Rules.ActorMaxHPs(seller) || seller.Inventory == m_Actor.Inventory) return false;
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            if (!m_Actor.Personality.Knowledge.Facts.Exists(f => f.Kind == "medicine_offered" && f.SubjectId == seller.PersonalityIdentity && f.OtherId == m_Actor.PersonalityIdentity && turn - f.EventTurn < 60)) return false;
            BaseAI ai = seller.Controller as BaseAI; if (ai == null || !ai.Directives.CanTrade) return false;
            medicine = null; payment = null;
            foreach (Item item in seller.Inventory.Items) if (item is ItemMedicine && ((ItemMedicine)item).Healing > 0 && !item.IsEquipped) { medicine = (ItemMedicine)item; break; }
            if (medicine == null) return false;
            var gift = new ItemMedicine(medicine.Model); string reason;
            if (!m_Actor.Inventory.CanAddAll(gift) || !m_Game.Rules.CanActorGiveItemTo(seller, m_Actor, gift, out reason)) return false;
            foreach (Item item in m_Actor.Inventory.Items)
                if (!(item is ItemMedicine) && !item.IsUnique && !item.IsEquipped && seller.Inventory.CanAddAll(item) &&
                    m_Game.Rules.CanActorGiveItemTo(m_Actor, seller, item, out reason) && ai.RateTradeOffer(m_Game, m_Actor, item, gift) != BaseAI.TradeRating.REFUSE) { payment = item; return true; }
            return false;
        }
        public override void Perform() { if (IsLegal()) m_Game.DoNpcMedicineTrade(m_Actor, seller, payment, medicine, goal); }
    }
}
