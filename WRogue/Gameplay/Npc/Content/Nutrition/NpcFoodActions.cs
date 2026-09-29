using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcFoodActions
    {
        public static ActorAction Take(NpcActionContext c)
        {
            Location place = c.Step == null ? c.Goal.Destination : c.Step.Place;
            return c.Action(() => {
                ItemFood food = c.Food ?? NpcPlanExecution.FoodOnGround(c.Game, c.Owner, place); string reason;
                return food != null && NpcPlanExecution.Near(c.Game, c.Owner, place) &&
                    NpcPlanExecution.FoodOnGround(c.Game, c.Owner, place) == food && c.Game.Rules.CanActorGetItem(c.Owner, food, out reason);
            }, () => {
                ItemFood food = c.Food ?? NpcPlanExecution.FoodOnGround(c.Game, c.Owner, place);
                long before = c.Owner.Inventory.TotalReceived;
                c.Game.DoTakeItem(c.Owner, place.Position, food, causeId: c.Goal.Plan != null && c.Goal.Plan.LastEventId > 0 ? c.Goal.Plan.LastEventId : c.Goal.CauseId, storyId: c.Goal.StoryId);
                if (c.Owner.Inventory.TotalReceived <= before) return;
                c.Goal.Progress = Math.Max(1, c.Goal.Progress);
                NpcResourceCompetition.Taken(c.Game, c.Owner, c.Goal, place, "food"); c.Publish("supplies_acquired");
                if (c.Step != null) c.Done(default(NpcPlanningState), retain: false);
            });
        }
        public static ActorAction Request(NpcActionContext c)
        {
            Guid person = c.Step == null ? c.Goal.TargetId : c.Step.Target;
            return c.Action(() => c.NearPerson(person) && !c.Owner.Personality.Knowledge.WasTold(-c.Goal.Sequence, person) &&
                (c.Step != null || !c.Goal.Announced && c.Game.Rules.IsActorHungry(c.Owner) && !NpcFoodSupply.HasFood(c.Game, c.Owner)), () => {
                c.Game.DoSay(c.Owner, c.Target, c.Goal.Generated == null || c.Goal.Generated.SubjectId == c.Owner.PersonalityIdentity ?
                    "Could you spare some food?" : "Could you spare food? I'm trying to help someone.", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                    c.Goal.CauseId, c.Goal.StoryId);
                if (c.Capability.WaitingAfterAnnouncement) { c.Goal.Announced = true; c.Goal.Status = NpcIntentStatus.Waiting; }
                c.Owner.Personality.Knowledge.Told(-c.Goal.Sequence, person, c.Owner.Location.Map.LocalTime.TurnCounter);
                c.Publish("requested_food", c.Target); c.Goal.NextAttempt = c.Owner.Location.Map.LocalTime.TurnCounter + 8;
            });
        }
        public static ActorAction Give(NpcActionContext c)
        {
            return c.Action(() => c.NearPerson(c.Goal.TargetId) && c.Goal.Progress < 2 &&
                (c.Food ?? NpcFoodSupply.SpareFood(c.Game, c.Owner, c.Target)) != null &&
                (c.Food == null || NpcFoodSupply.SpareFood(c.Game, c.Owner, c.Target) == c.Food), () => {
                ItemFood food = c.Food ?? NpcFoodSupply.SpareFood(c.Game, c.Owner, c.Target);
                bool needed = c.Game.Rules.IsActorHungry(c.Target);
                if (!c.Target.Inventory.AddAll(NpcFoodSupply.OneFood(food))) return;
                c.Owner.Inventory.Consume(food); c.Game.SpendActorActionPoints(c.Owner, Rules.BASE_ACTION_COST);
                if (needed) NpcPlanExecution.Publish(c.Game, "helped", c.Target, c.Owner, c.Goal);
                c.Game.DoSay(c.Owner, c.Target, c.Capability.DeliveryText ?? "Here, I can spare some food.", RogueGame.Sayflags.IS_FREE_ACTION);
                c.Publish("shared_food", c.Target);
                NpcValueDefinition value = c.Goal.Generated == null ? null : c.Game.NpcContent.Value(c.Goal.Generated);
                bool repeat = value != null && value.AfterDelivery != null && value.AfterDelivery(c);
                if (c.Capability.ReportAfterDelivery) { c.Goal.Progress = 2; if (c.Step != null) c.Done((ulong)NpcPlanFact.Delivered); return; }
                c.Done((ulong)NpcPlanFact.Delivered, repeat);
            });
        }
    }
}
