using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcBarter : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        readonly Actor seller;
        Item payment;
        ItemFood food;
        public ActionNpcBarter(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step, Actor seller) : base(actor, game)
        { this.goal = goal; this.step = step; this.seller = seller; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step || step.Action != NpcPlanAction.BarterFood ||
                seller == null || seller.PersonalityIdentity != step.Target || seller == m_Actor || seller.IsPlayer || seller.IsSleeping ||
                !NpcIntentSystem.CanSee(m_Game, m_Actor, seller) || !NpcPlanExecution.Near(m_Game, m_Actor, seller.Location) ||
                !m_Game.Rules.CanActorInitiateTradeWith(m_Actor, seller) || m_Game.Rules.IsActorHungry(seller) || m_Actor.Inventory == seller.Inventory ||
                (goal.Plan.Desired == (ulong)NpcPlanFact.Food && NpcFoodSupply.HasFood(m_Game, m_Actor))) return false;
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            if (!m_Actor.Personality.Knowledge.Facts.Exists(f => f.Kind == "food_offered" && f.SubjectId == seller.PersonalityIdentity &&
                f.OtherId == m_Actor.PersonalityIdentity && turn - f.EventTurn <= 60)) return false;
            BaseAI ai = seller.Controller as BaseAI;
            if (ai == null || !ai.Directives.CanTrade) return false;
            food = null; payment = null;
            foreach (Item item in seller.Inventory.Items)
                if (item is ItemFood && item.Quantity >= 3 && !item.IsEquipped && !item.IsUnique && !m_Game.Rules.IsFoodSpoiled((ItemFood)item, turn))
                { food = (ItemFood)item; break; }
            if (food == null) return false;
            ItemFood gift = NpcFoodSupply.OneFood(food); gift.Quantity = 2; string reason;
            if (!m_Actor.Inventory.CanAddAll(gift) || !m_Game.Rules.CanActorGiveItemTo(seller, m_Actor, gift, out reason)) return false;
            foreach (Item item in m_Actor.Inventory.Items)
                if (!(item is ItemFood) && !item.IsUnique && !item.IsEquipped && seller.Inventory.CanAddAll(item) && m_Game.Rules.CanActorGiveItemTo(m_Actor, seller, item, out reason) &&
                    ai.RateTradeOffer(m_Game, m_Actor, item, gift) != BaseAI.TradeRating.REFUSE) { payment = item; return true; }
            return false;
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoNpcBarter(m_Actor, seller, payment, food, goal);
            if (!goal.Finished) goal.Plan.Cursor++;
        }
    }
}
