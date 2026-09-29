using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcGoalLifecycle
    {
        public static void Apply(Actor owner, List<NpcGoalCandidate> candidates, NpcContentCatalog catalog, bool maintain)
        {
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.Generated != null)
                {
                    NpcGoalCandidate fresh = candidates.Find(c => c.State.Key == intent.Generated.Key);
                    if (fresh != null)
                    {
                        if (intent.Plan != null && intent.Generated.Current != fresh.State.Current)
                        { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = owner.Location.Map.LocalTime.TurnCounter; intent.NextAttempt = owner.Location.Map.LocalTime.TurnCounter; }
                        intent.Generated = fresh.State;
                    }
                    else { intent.Generated.Utility = 0; intent.Generated.EvaluatedTurn = owner.Location.Map.LocalTime.TurnCounter; }
                }
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (maintain && !intent.Finished && intent.Generated != null && intent.Generated.Utility < NpcGoalGenerator.MinimumUtility)
                {
                    NpcValueDefinition value = catalog.Value(intent.Generated);
                    bool satisfied = intent.Generated.Deficit == 0 && value != null && value.CompleteWhenSatisfied;
                    NpcIntentSystem.Finish(owner, intent, satisfied ? NpcIntentStatus.Completed : NpcIntentStatus.Abandoned,
                        satisfied ? "observed that the desired state was satisfied" : "motivation changed");
                }
            candidates.Sort((a, b) => { int score = b.State.Utility.CompareTo(a.State.Utility);
                return score != 0 ? score : String.CompareOrdinal(a.State.Key, b.State.Key); });
            foreach (NpcGoalCandidate candidate in candidates)
            {
                if (candidate.State.Utility < NpcGoalGenerator.MinimumUtility || HasEquivalent(owner, candidate, catalog)) continue;
                int turn = owner.Location.Map.LocalTime.TurnCounter;
                if (!CooldownReady(owner, candidate, catalog, turn)) continue;
                if (!owner.Personality.CanGenerateGoal(candidate.State.Key, owner.Location.Map.LocalTime.TurnCounter))
                {
                    if (!maintain) continue;
                    NpcIntent weakest = null; int active = 0;
                    foreach (NpcIntent intent in owner.Personality.Intents)
                        if (!intent.Finished)
                        {
                            active++;
                            if (intent.Generated != null && (weakest == null || intent.Generated.Utility < weakest.Generated.Utility)) weakest = intent;
                        }
                    if (active < 4 || weakest == null || candidate.State.Utility < weakest.Generated.Utility + 10 ||
                        !owner.Personality.GoalCooldownReady(candidate.State.Key, owner.Location.Map.LocalTime.TurnCounter)) continue;
                    NpcIntentSystem.Finish(owner, weakest, NpcIntentStatus.Abandoned, "a more important state became unsatisfied");
                }
                Start(owner, candidate.Target, candidate.Capability, candidate.Cause, candidate.Story, candidate.State);
            }
        }
        static bool HasEquivalent(Actor owner, NpcGoalCandidate candidate, NpcContentCatalog catalog)
        {
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && ((intent.Generated != null && intent.Generated.Key == candidate.State.Key) ||
                    (intent.DefinitionId == candidate.Capability.Id && intent.TargetId == candidate.Target.Id && (intent.Generated == null || candidate.State.Resource == null)) ||
                    (catalog.Value(candidate.State) != null && Array.IndexOf(catalog.Value(candidate.State).EquivalentCapabilities, intent.DefinitionId) >= 0))) return true;
            return false;
        }
        public static bool CooldownReady(Actor owner, NpcGoalCandidate candidate, NpcContentCatalog catalog, int turn)
        {
            if (!owner.Personality.LegacyCooldownReady(candidate.Capability.Id, turn)) return false;
            NpcValueDefinition value = catalog.Value(candidate.State);
            if (value != null) foreach (string alias in value.CooldownAliases)
                if (!owner.Personality.LegacyCooldownReady(alias, turn)) return false;
            return true;
        }
        public static NpcIntent Start(Actor owner, NpcKnownPerson target, NpcIntentDefinition definition,
            long cause = 0, string parent = null, NpcGeneratedGoal generated = null,
            Location destination = default(Location), Guid groupId = default(Guid))
        {
            if (!NpcIntentSystem.Enabled(owner) || target == null || target.Dead || target.Place.Map == null) return null;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            if (generated == null ? !owner.Personality.CanStartIntent(definition.Id, turn) : !owner.Personality.CanGenerateGoal(generated.Key, turn)) return null;
            RelationshipRecord opinion = owner.Personality.Person(target.Id); int attitude = opinion == null ? 0 : opinion.Feeling;
            int score = generated == null && definition.AssignedScore != null ? definition.AssignedScore(owner, target.Id) : generated == null ? definition.ScoreKnown(owner, attitude, owner.Leader != null && owner.Leader.PersonalityIdentity == target.Id) + definition.SocialScore(owner, target.Id) : generated.Utility;
            if (score < (generated == null ? definition.Threshold : NpcGoalGenerator.MinimumUtility)) return null;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory prior = parent == null ? null : director.Find(parent);
                string id = generated == null ? parent : parent != null && prior != null && !prior.Finished ? parent : null;
                id = id ?? owner.PersonalityIdentity.ToString("N") + ":" + owner.Personality.NextIntentSequence;
                string key = generated == null ? definition.Id + ":" + owner.PersonalityIdentity + ":" + target.Id : "value:" + owner.PersonalityIdentity + ":" + generated.Key;
                NpcStory story = director.Open(id, definition.Id, owner, cause, turn + definition.Duration, key);
                if (story == null || story.Roles.Count >= 8) return null;
                if (definition.CompleteEpisodeOnSuccess) story.CompletionGoal = definition.Id;
                NpcIntent intent = owner.Personality.StartGoal(definition.Id, owner, target, turn,
                    definition.Duration, definition.Cooldown, score, cause, id, attitude, generated);
                if (intent == null) return null;
                intent.Destination = generated != null && generated.ObjectPlace.Map != null ? generated.ObjectPlace : destination;
                intent.GroupId = groupId; director.Bind(story, owner, intent);
                if (generated != null)
                {
                    director.Link(story, parent, owner, cause);
                    foreach (NpcFact fact in owner.Personality.Knowledge.Facts)
                        if (generated.Causes != null && Array.IndexOf(generated.Causes, fact.EventId) >= 0) director.Link(story, fact.StoryId, owner, fact.EventId);
                }
                Session.Get.ResidentRecords.IntentChanged(owner, intent, "started", generated == null ? definition.Name : generated.Explanation);
                return intent;
            }
        }
        public static void KnownDeath(Actor owner, Guid subject, string reason, NpcContentCatalog catalog)
        {
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.TargetId == subject) TargetDied(owner, intent, false, reason, catalog);
        }
        public static void TargetDied(Actor owner, NpcIntent intent, bool killedByOwner, string reason, NpcContentCatalog catalog)
        {
            NpcIntentDefinition definition = catalog.Capability(intent.DefinitionId);
            NpcIntentOutcome outcome = definition == null || definition.OnTargetDeath == null ? null : definition.OnTargetDeath(killedByOwner);
            Finish(owner, intent, outcome == null ? NpcIntentStatus.Failed : outcome.Status, outcome == null ? reason : outcome.Reason);
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
    }
}
