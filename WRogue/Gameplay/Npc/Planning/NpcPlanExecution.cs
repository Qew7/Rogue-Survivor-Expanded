using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcPlanExecution
    {
        public static void Succeeded(NpcActionContext context, NpcPlanningState observed, bool repeat, bool retain)
        {
            NpcIntent goal = context.Goal; if (goal.Finished) return;
            if (repeat)
            {
                goal.Progress = 0;
                if (goal.Plan != null) { goal.Plan.Invalidate(); if (goal.Plan.CompletedFacts != null) goal.Plan.CompletedFacts.Clear(); goal.Plan.NextPlanningTurn = context.Owner.Location.Map.LocalTime.TurnCounter; }
                return;
            }
            if (goal.Plan == null) { NpcIntentSystem.Finish(context.Game.NpcContent, context.Owner, goal,
                NpcIntentStatus.Completed, "performed the intended action"); return; }
            if (retain && !observed.Empty)
            {
                if (goal.Plan.CompletedFacts == null) goal.Plan.CompletedFacts = new System.Collections.Generic.List<string>();
                foreach (string name in context.Game.NpcContent.Facts.Names(observed))
                    if (goal.Plan.CompletedFacts.Count < 32 && !goal.Plan.CompletedFacts.Contains(name)) goal.Plan.CompletedFacts.Add(name);
            }
            var domain = new NpcPlanDomain(context.Game, context.Owner, goal, context.Visible);
            if ((domain.InitialState | observed).Contains(goal.Plan.DesiredState))
                NpcIntentSystem.Finish(context.Game.NpcContent, context.Owner, goal,
                    NpcIntentStatus.Completed, "actually satisfied the desired state");
            else if (context.Step != null && goal.Plan.Current == context.Step) goal.Plan.Cursor++;
        }
        public static bool Owned(RogueGame game, Actor owner, NpcIntent goal)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.IsSleeping || goal == null || goal.Finished || goal.Status == NpcIntentStatus.Paused ||
                !owner.Personality.Intents.Contains(goal)) return false;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            NpcIntentDefinition definition = game.NpcContent.Capability(goal.DefinitionId);
            return definition != null && definition.Score(owner, goal, game.NpcContent) >= definition.ThresholdFor(goal) && turn < goal.Deadline && turn >= goal.NextAttempt &&
                (goal.GroupId == Guid.Empty || owner.SocialGroup != null && owner.SocialGroup.Identity == goal.GroupId);
        }
        public static ItemFood FoodOnGround(RogueGame game, Actor owner, Location place)
        {
            if (!NpcKnowledgeSystem.Visible(game, owner, place)) return null;
            Inventory ground = place.Map.GetItemsAt(place.Position); if (ground == null) return null;
            foreach (Item item in ground.Items)
                if (item is ItemFood && !game.Rules.IsFoodSpoiled((ItemFood)item, owner.Location.Map.LocalTime.TurnCounter)) return (ItemFood)item;
            return null;
        }
        public static bool Near(RogueGame game, Actor owner, Location place)
        { return owner.Location.Map == place.Map && game.Rules.GridDistance(owner.Location.Position, place.Position) <= 1; }
        public static ItemMedicine Medicine(RogueGame game, Actor owner, Location place, bool owned = false)
        {
            if (!owned && !NpcKnowledgeSystem.Visible(game, owner, place)) return null;
            Inventory items = owned ? owner.Inventory : place.Map.GetItemsAt(place.Position);
            if (items != null) foreach (Item item in items.Items)
                if (item is ItemMedicine && ((ItemMedicine)item).Healing > 0 && !item.IsEquipped) return (ItemMedicine)item;
            return null;
        }
        public static SignificantEvent Publish(RogueGame game, string kind, Actor owner, Actor other, NpcIntent goal)
        {
            long cause = goal.Plan != null && goal.Plan.LastEventId > 0 ? goal.Plan.LastEventId : goal.CauseId;
            var source = new SignificantEvent(kind, owner, other, owner.Location.Map, owner.Location.Position,
                owner.Location.Map.LocalTime.TurnCounter, causeId: cause, storyId: goal.StoryId)
                { Resource = goal.Generated == null ? null : goal.Generated.Resource, ResourcePlace = goal.Generated == null ? default(Location) : goal.Generated.ObjectPlace };
            PersonalitySystem.Report(game, source);
            if (goal.Plan != null && source.Id > goal.Plan.LastEventId) goal.Plan.LastEventId = source.Id;
            return source;
        }
        public static bool OfferTrade(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (source.Kind != "requested_food" || source.Other != owner || source.Subject == null ||
                PersonalitySystem.Bias(owner, DecisionKind.Trade) < 15 || PersonalitySystem.Bias(owner, DecisionKind.Compassion) > 0 ||
                owner.Personality.Reactions.Count >= 4 || game.Rules.AreEnemies(owner, source.Subject) || game.Rules.IsActorHungry(owner)) return false;
            bool available = false;
            foreach (Item item in owner.Inventory.Items)
                if (item is ItemFood && item.Quantity >= 3 && !item.IsUnique && !item.IsEquipped && !game.Rules.IsFoodSpoiled((ItemFood)item, source.Turn)) available = true;
            if (!available) return false;
            owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I'll exchange two food units for other supplies.", source.Id,
                source.Turn, "food_offered", source.StoryId)); return true;
        }
    }
}
