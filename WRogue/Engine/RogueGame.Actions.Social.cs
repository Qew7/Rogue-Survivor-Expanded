using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        internal void DoNpcMedicalAid(Actor actor, Actor target, ItemMedicine medicine, bool treat, NpcIntent goal)
        {
            if (!treat && !target.Inventory.AddAll(new ItemMedicine(medicine.Model))) return;
            int previousHP = target.HitPoints;
            if (treat) target.HitPoints = Math.Min(m_Rules.ActorMaxHPs(target), target.HitPoints + m_Rules.ActorMedicineEffect(actor, medicine.Healing));
            actor.Inventory.Consume(medicine); SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            DoSay(actor, target, treat ? "Let me treat those wounds." : "Take this medicine. You need it.", Sayflags.IS_FREE_ACTION);
            SignificantEvent source = NpcPlanExecution.Publish(this, treat ? "treated_person" : "shared_medicine", actor, target, goal);
            if (treat && previousHP < target.HitPoints && target.Personality != null)
                foreach (NpcIntent recovery in target.Personality.Intents)
                    if (!recovery.Finished && NpcContent.Capability(recovery.DefinitionId) != null &&
                        NpcContent.Capability(recovery.DefinitionId).GetResult(NpcContent, recovery.Generated).Contains((ulong)NpcPlanFact.Healthy))
                    {
                        if (recovery.Generated != null) recovery.Generated.Causes = new[] { source.Id };
                        NpcStory episode = Session.Get.NpcDirector.Find(recovery.StoryId);
                        if (episode != null) Session.Get.NpcDirector.Link(episode, goal.StoryId, target, source.Id);
                        if (target.HitPoints >= m_Rules.ActorMaxHPs(target))
                        {
                            if (recovery.Plan == null) recovery.Plan = new NpcPlan { DesiredState = NpcContent.Capability(recovery.DefinitionId).GetResult(NpcContent, recovery.Generated) };
                            new NpcActionContext(this, target, recovery).Done(default(NpcPlanningState), retain: false);
                        }
                        else if (recovery.Plan != null) { recovery.Plan.Invalidate(); recovery.Plan.NextPlanningTurn = target.Location.Map.LocalTime.TurnCounter; }
                    }

        }
        internal void DoNpcMedicineTrade(Actor buyer, Actor seller, Item payment, ItemMedicine medicine, NpcIntent goal)
        {
            var gift = new ItemMedicine(medicine.Model);
            buyer.Inventory.RemoveAllQuantity(payment); seller.Inventory.AddAll(payment);
            seller.Inventory.Consume(medicine); buyer.Inventory.AddAll(gift);
            SpendActorActionPoints(buyer, Rules.BASE_ACTION_COST);
            DoSay(buyer, seller, "Agreed. Supplies for medicine.", Sayflags.IS_FREE_ACTION);
            NpcPlanExecution.Publish(this, "bartered_medicine", buyer, seller, goal);
            goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = buyer.Location.Map.LocalTime.TurnCounter;
        }
    }
}
