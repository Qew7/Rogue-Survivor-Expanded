using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcIntentSystem
    {
        public static bool Enabled(Actor actor)
        { return Session.Get.GamePreset.NpcPersonalitiesEnabled && actor != null && !actor.IsPlayer && !actor.IsDead &&
            actor.Personality != null && actor.Model != null && actor.Controller is Gameplay.AI.OrderableAI &&
            actor.Model.Abilities.IsIntelligent && !actor.Model.Abilities.IsUndead; }
        public static void Observe(RogueGame game, Actor observer, SignificantEvent source)
        {
            if (!Enabled(observer)) return;
            PersonalityState state = observer.Personality;
            if (source.Id > 0 && source.Id <= state.LastIntentEventId) return;
            state.LastIntentEventId = source.Id;
            if (source.Kind == "death")
            {
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && source.Subject != null &&
                        (source.Subject == observer || (intent.Generated != null ? intent.Generated.SubjectId == source.Subject.PersonalityIdentity :
                            intent.TargetId == source.Subject.PersonalityIdentity || intent.CoordinatorId == source.Subject.PersonalityIdentity)))
                        Finish(observer, intent, NpcIntentStatus.Failed, source.Subject == observer ? "owner died" : "learned that the target died");
                if (source.Subject == observer) state.Reactions.Clear();
            }
            if (source.Kind == "helped" && source.Subject == observer && source.Other != null)
            {
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && (intent.DefinitionId == NpcIntentContent.Request.Id || intent.DefinitionId == NpcIntentContent.Obtain.Id) &&
                        (HasFood(game, observer) || !game.Rules.IsActorHungry(observer)))
                        Finish(observer, intent, NpcIntentStatus.Completed, "received needed supplies");
                if (state.Reactions.Count < 4)
                {
                    string text = PersonalitySystem.Bias(observer, DecisionKind.Compassion) < 0 ? "About time." :
                        PersonalitySystem.Bias(observer, DecisionKind.Group) < 0 ? "Thanks." :
                        "Thank you, " + source.Other.UnmodifiedName + ". I needed that.";
                    state.Reactions.Add(new NpcReaction(source.Other, text, source.Id, source.Turn, storyId: source.StoryId));
                }
            }
            if (source.Kind == "request_refused" && source.Other == observer && source.Subject != null)
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && intent.DefinitionId == NpcIntentContent.Request.Id &&
                        intent.TargetId == source.Subject.PersonalityIdentity && intent.StoryId == source.StoryId)
                    {
                        if (intent.Plan == null) Finish(observer, intent, NpcIntentStatus.Failed, "request was declined");
                        else { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = source.Turn; intent.NextAttempt = source.Turn + 1; intent.Status = NpcIntentStatus.Active; }
                    }
            if (NpcPlanExecution.OfferTrade(game, observer, source)) return;
            NpcGoalGenerator.Refresh(game, observer);
            if (source.Kind == "requested_food" && source.Other == observer && source.Subject != null &&
                !game.Rules.AreEnemies(observer, source.Subject))
            {
                bool answering = false;
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && intent.DefinitionId == NpcIntentContent.Help.Id && intent.TargetId == source.Subject.PersonalityIdentity) answering = true;
                if (!answering && state.Reactions.Count < 4)
                    state.Reactions.Add(new NpcReaction(source.Subject, "I'm keeping my supplies.", source.Id,
                        source.Turn, "request_refused", source.StoryId));
            }
        }
        public static void Finish(Actor owner, NpcIntent intent, NpcIntentStatus status, string reason)
        {
            if (intent.Finished) return;
            intent.Status = status; intent.Outcome = reason;
            intent.FinishedTurn = owner.Location.Map == null ? intent.StartedTurn : owner.Location.Map.LocalTime.TurnCounter;
            Session.Get.ResidentRecords.IntentChanged(owner, intent, status.ToString().ToLowerInvariant(), reason);
            Session.Get.NpcDirector.Outcome(owner, intent);
            NpcStorySystem.GoalFinished(owner, intent);
            intent.LastKnown = default(Location);
            intent.Destination = default(Location);
            intent.CoordinatorPlace = default(Location);
            if (intent.Plan != null) intent.Plan.Release();
        }
        public static bool HasFood(RogueGame game, Actor actor)
        {
            if (actor.Inventory == null) return false;
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemFood && item.Quantity > 0 && !game.Rules.IsFoodSpoiled((ItemFood)item,
                    actor.Location.Map.LocalTime.TurnCounter)) return true;
            return false;
        }
        public static SignificantEvent Publish(RogueGame game, string kind, Actor subject, Actor other, long cause = 0, string story = null)
        {
            SignificantEvent source = new SignificantEvent(kind, subject, other, subject.Location.Map,
                subject.Location.Position, subject.Location.Map.LocalTime.TurnCounter, causeId: cause, storyId: story);
            PersonalitySystem.Report(game, source); return source;
        }
    }
}
