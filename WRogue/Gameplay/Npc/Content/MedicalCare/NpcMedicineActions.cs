using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcMedicineActions
    {
        public static ActorAction Take(NpcActionContext c)
        { return c.Action(() => {
            ItemMedicine medicine = NpcPlanExecution.Medicine(c.Game, c.Owner, c.Step.Place); string reason;
            return medicine != null && NpcPlanExecution.Near(c.Game, c.Owner, c.Step.Place) && c.Game.Rules.CanActorGetItem(c.Owner, medicine, out reason);
        }, () => {
            long before = c.Owner.Inventory.TotalReceived;
            c.Game.DoTakeItem(c.Owner, c.Step.Place.Position, NpcPlanExecution.Medicine(c.Game, c.Owner, c.Step.Place), causeId: c.Goal.CauseId, storyId: c.Goal.StoryId);
            if (before == c.Owner.Inventory.TotalReceived) return;
            NpcResourceCompetition.Taken(c.Game, c.Owner, c.Goal, c.Step.Place, "medicine"); c.Publish("medicine_acquired");
            c.Done(default(NpcPlanningState), retain: false);
        }); }
        public static ActorAction Use(NpcActionContext c)
        { return c.Action(() => {
            ItemMedicine medicine = NpcPlanExecution.Medicine(c.Game, c.Owner, c.Owner.Location, true); string reason;
            return c.Owner.HitPoints < c.Game.Rules.ActorMaxHPs(c.Owner) && medicine != null && c.Game.Rules.CanActorUseItem(c.Owner, medicine, out reason);
        }, () => {
            c.Game.DoUseItem(c.Owner, NpcPlanExecution.Medicine(c.Game, c.Owner, c.Owner.Location, true), c.Goal);
            if (!c.Goal.Finished) { c.Goal.Plan.Invalidate(); c.Goal.Plan.NextPlanningTurn = c.Owner.Location.Map.LocalTime.TurnCounter; }
        }); }
        public static ActorAction Ask(NpcActionContext c)
        { return c.Action(() => c.NearPerson(c.Step.Target) && !c.Owner.Personality.Knowledge.WasTold(-c.Goal.Sequence, c.Step.Target) &&
            NpcPlanExecution.Medicine(c.Game, c.Owner, c.Owner.Location, true) == null, () => {
            c.Game.DoSay(c.Owner, c.Target, "Could you spare medicine? I'm trying to get treatment.", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                c.Goal.CauseId, c.Goal.StoryId);
            c.Owner.Personality.Knowledge.Told(-c.Goal.Sequence, c.Step.Target, c.Owner.Location.Map.LocalTime.TurnCounter);
            c.Publish("requested_medicine", c.Target); c.Goal.Plan.Invalidate(); c.Goal.NextAttempt = c.Owner.Location.Map.LocalTime.TurnCounter + 8;
        }); }
        public static ActorAction Give(NpcActionContext c) { return Aid(c, false); }
        public static ActorAction Treat(NpcActionContext c) { return Aid(c, true); }
        static ActorAction Aid(NpcActionContext c, bool treat)
        { return c.Action(() => {
            if (!c.NearPerson(c.Step.Target)) return false;
            ItemMedicine medicine = NpcPlanExecution.Medicine(c.Game, c.Owner, c.Owner.Location, true); string reason;
            if (medicine == null) return false;
            if (treat) return c.Target.HitPoints < c.Game.Rules.ActorMaxHPs(c.Target);
            var gift = new ItemMedicine(medicine.Model);
            return c.Target.Inventory != null && c.Target.Inventory != c.Owner.Inventory && c.Target.Inventory.CanAddAll(gift) &&
                c.Game.Rules.CanActorGiveItemTo(c.Owner, c.Target, gift, out reason);
        }, () => {
            c.Game.DoNpcMedicalAid(c.Owner, c.Target, NpcPlanExecution.Medicine(c.Game, c.Owner, c.Owner.Location, true), treat, c.Goal);
            if (!treat || c.Target.HitPoints >= c.Game.Rules.ActorMaxHPs(c.Target))
            { if (c.Capability.ReportAfterDelivery) c.Goal.Progress = 2; c.Done((ulong)(NpcPlanFact.Helped | NpcPlanFact.Delivered)); }
            else { c.Goal.Plan.Invalidate(); c.Goal.Plan.NextPlanningTurn = c.Owner.Location.Map.LocalTime.TurnCounter; }
        }); }
    }
}
