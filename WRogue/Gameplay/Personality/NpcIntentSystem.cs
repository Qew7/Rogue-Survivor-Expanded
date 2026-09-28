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
        static NpcIntent Start(RogueGame game, Actor owner, Actor target, NpcIntentDefinition definition,
            int turn, long cause = 0, string story = null)
        {
            if (!Enabled(owner) || target == null || target == owner || target.IsDead ||
                target.Model.Abilities.IsUndead || !target.Model.Abilities.IsIntelligent) return null;
            int score = definition.Score(owner, target); if (score < definition.Threshold) return null;
            NpcIntent intent = owner.Personality.StartIntent(definition.Id, owner, target, turn,
                definition.Duration, definition.Cooldown, score, cause, story, PersonalitySystem.Attitude(owner, target));
            if (intent != null) Session.Get.ResidentRecords.IntentChanged(owner, intent, "started", definition.Name);
            return intent;
        }
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
                        (source.Subject == observer || intent.TargetId == source.Subject.PersonalityIdentity))
                        Finish(observer, intent, NpcIntentStatus.Failed, source.Subject == observer ? "owner died" : "learned that the target died");
                if (source.Subject == observer) state.Reactions.Clear();
            }
            if (source.Kind == "helped" && source.Subject == observer && source.Other != null)
            {
                foreach (NpcIntent intent in state.Intents)
                    if (!intent.Finished && intent.DefinitionId == NpcIntentContent.Request.Id &&
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
                        Finish(observer, intent, NpcIntentStatus.Failed, "request was declined");
            NpcIntent answer = null;
            foreach (NpcIntentDefinition definition in NpcIntentContent.ForEvent(source.Kind))
            {
                Actor target = definition.Bind(observer, source);
                if (target == null || (definition.Method != NpcIntentMethod.LeaveGroup && game.Rules.AreEnemies(observer, target))) continue;
                NpcIntent intent = Start(game, observer, target, definition, source.Turn, source.Id, source.StoryId);
                if (definition == NpcIntentContent.Help) answer = intent;
            }
            if (source.Kind == "requested_food" && source.Other == observer && source.Subject != null &&
                !game.Rules.AreEnemies(observer, source.Subject))
            {
                if (answer == null && state.Reactions.Count < 4)
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
            intent.LastKnown = default(Location);
        }
        public static bool HasFood(RogueGame game, Actor actor)
        {
            if (actor.Inventory == null) return false;
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemFood && item.Quantity > 0 && !game.Rules.IsFoodSpoiled((ItemFood)item,
                    actor.Location.Map.LocalTime.TurnCounter)) return true;
            return false;
        }
        public static void ConsiderFoodRequest(RogueGame game, Actor actor, IList<Actor> visible)
        {
            if (!Enabled(actor) || !game.Rules.IsActorHungry(actor) || HasFood(game, actor) ||
                !actor.Personality.CanStartIntent(NpcIntentContent.Request.Id, actor.Location.Map.LocalTime.TurnCounter)) return;
            Actor best = null; int bestScore = Int32.MinValue;
            foreach (Actor candidate in visible)
            {
                if (candidate == actor || candidate.IsDead || candidate.IsSleeping || candidate.Model.Abilities.IsUndead ||
                    !candidate.Model.Abilities.IsIntelligent || game.Rules.AreEnemies(actor, candidate)) continue;
                int score = NpcIntentContent.Request.Score(actor, candidate) - game.Rules.GridDistance(actor.Location.Position, candidate.Location.Position);
                if (candidate == actor.Leader) score += 10;
                if (best == null || score > bestScore || (score == bestScore && candidate.PersonalityIdentity.CompareTo(best.PersonalityIdentity) < 0))
                { best = candidate; bestScore = score; }
            }
            if (best != null) Start(game, actor, best, NpcIntentContent.Request, actor.Location.Map.LocalTime.TurnCounter);
        }
        public static SignificantEvent Publish(RogueGame game, string kind, Actor subject, Actor other, long cause = 0, string story = null)
        {
            SignificantEvent source = new SignificantEvent(kind, subject, other, subject.Location.Map,
                subject.Location.Position, subject.Location.Map.LocalTime.TurnCounter, causeId: cause, storyId: story);
            PersonalitySystem.Report(game, source); return source;
        }
    }
}
