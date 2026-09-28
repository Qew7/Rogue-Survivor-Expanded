using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcPlanExecution
    {
        public static bool Owned(RogueGame game, Actor owner, NpcIntent goal)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.IsSleeping || goal == null || goal.Finished || goal.Status == NpcIntentStatus.Paused ||
                !owner.Personality.Intents.Contains(goal)) return false;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            NpcIntentDefinition definition = NpcIntentContent.Find(goal.DefinitionId);
            return definition != null && definition.Score(owner, goal) >= definition.Threshold && turn < goal.Deadline && turn >= goal.NextAttempt &&
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
        public static SignificantEvent Publish(RogueGame game, string kind, Actor owner, Actor other, NpcIntent goal)
        {
            long cause = goal.Plan != null && goal.Plan.LastEventId > 0 ? goal.Plan.LastEventId : goal.CauseId;
            SignificantEvent source = NpcIntentSystem.Publish(game, kind, owner, other, cause, goal.StoryId);
            if (goal.Plan != null) goal.Plan.LastEventId = source.Id;
            return source;
        }
        public static void ConsiderNeed(RogueGame game, Actor owner)
        {
            if (!NpcIntentSystem.Enabled(owner) || !game.Rules.IsActorHungry(owner) || NpcIntentSystem.HasFood(game, owner)) return;
            if (!owner.Personality.CanStartIntent(NpcIntentContent.Request.Id, owner.Location.Map.LocalTime.TurnCounter)) return;
            foreach (NpcIntent goal in owner.Personality.Intents)
                if (!goal.Finished && (goal.DefinitionId == NpcIntentContent.Request.Id || goal.DefinitionId == NpcIntentContent.Obtain.Id)) return;
            if (owner.Personality.HasKnowledge && owner.Location.Map.LocalTime.TurnCounter < owner.Personality.Knowledge.NextPlanTurn) return;
            NpcStorySystem.StartKnown(owner, new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location }, NpcIntentContent.Obtain);
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
