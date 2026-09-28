using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcStorySystem
    {
        public static NpcIntent StartKnown(Actor owner, NpcKnownPerson target, NpcIntentDefinition definition,
            long cause = 0, string storyId = null, Location destination = default(Location), Guid groupId = default(Guid))
        {
            if (!NpcIntentSystem.Enabled(owner) || target == null || target.Dead || target.Place.Map == null) return null;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            if (!owner.Personality.CanStartIntent(definition.Id, turn)) return null;
            RelationshipRecord opinion = owner.Personality.Person(target.Id);
            int attitude = opinion == null ? 0 : opinion.Feeling;
            int score = definition.ScoreKnown(owner, attitude, owner.Leader != null && owner.Leader.PersonalityIdentity == target.Id) + definition.SocialScore(owner, target.Id);
            if (score < definition.Threshold) return null;
            string id = storyId ?? owner.PersonalityIdentity.ToString("N") + ":" + owner.Personality.NextIntentSequence;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory story = director.Open(id, definition.Id, owner, cause, turn + definition.Duration,
                    definition.Id + ":" + owner.PersonalityIdentity + ":" + target.Id);
                if (story == null || story.Roles.Count >= 8) return null;
                NpcIntent intent = owner.Personality.StartKnownIntent(definition.Id, owner, target, turn, definition.Duration,
                    definition.Cooldown, score, cause, id, attitude);
                intent.Destination = destination; intent.GroupId = groupId;
                director.Bind(story, owner, intent);
                Session.Get.ResidentRecords.IntentChanged(owner, intent, "started", definition.Name);
                return intent;
            }
        }
        public static void Consider(RogueGame game, Actor owner, IList<Actor> visible)
        {
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            if (turn < knowledge.NextPlanTurn) return;
            knowledge.NextPlanTurn = turn + 15;
            foreach (NpcFact fact in knowledge.Facts)
            {
                NpcSituationDefinition situation = NpcStoryContent.ForFact(fact.Kind);
                if (situation == null || turn - fact.EventTurn > situation.MaxAge || fact.OtherId == owner.PersonalityIdentity ||
                    fact.OtherId == Guid.Empty || fact.Confidence < situation.Confidence) continue;
                NpcKnownPerson target = knowledge.Person(fact.OtherId) ?? new NpcKnownPerson { Id = fact.OtherId, Name = fact.OtherName, Place = fact.Place, SeenTurn = fact.EventTurn };
                NpcIntentDefinition method = null; int best = Int32.MinValue;
                foreach (NpcIntentDefinition candidate in situation.Methods)
                { int score = candidate.ScoreKnown(owner, 0, false) + candidate.SocialScore(owner, target.Id);
                    if (score >= candidate.Threshold && score > best) { method = candidate; best = score; } }
                if (method == null) continue;
                if (method.ScoreKnown(owner, 0, false) + method.SocialScore(owner, target.Id) < method.Threshold ||
                    !owner.Personality.CanStartIntent(method.Id, turn)) continue;
                if (!Session.Get.NpcDirector.OfferDue(owner.Location.Map, turn)) return;
                StartKnown(owner, target, method, fact.EventId); return;
            }
            if (owner.SocialGroup == null) return;
            foreach (NpcKnownPerson person in knowledge.People)
                if (owner.SocialGroup.Members.Contains(person.Id) && person.Id != owner.PersonalityIdentity && !person.Dead &&
                    turn - person.SeenTurn >= 30 && NpcIntentSystem.VisibleTarget(visible, person.Id) == null &&
                    owner.Personality.CanStartIntent(NpcIntentContent.Seek.Id, turn) &&
                    NpcIntentContent.Seek.ScoreKnown(owner, 0, false) + NpcIntentContent.Seek.SocialScore(owner, person.Id) >= NpcIntentContent.Seek.Threshold)
                {
                    if (!Session.Get.NpcDirector.OfferDue(owner.Location.Map, turn)) return;
                    // Missing is an inference from old contact, not knowledge of current movement/death.
                    NpcFact inference = new NpcFact { Kind = "missing_companion", EventId = Session.Get.NextPersonalityEventId(),
                        SubjectId = person.Id, SubjectName = person.Name, Place = person.Place, EventTurn = turn,
                        LearnedTurn = turn, Confidence = 60, Source = NpcKnowledgeSource.Inferred, SourceId = owner.PersonalityIdentity };
                    knowledge.Learn(inference); Session.Get.ResidentRecords.InferredMissing(owner, inference);
                    StartKnown(owner, person, NpcIntentContent.Seek, inference.EventId); return;
                }
        }
        public static void EventFinished(RogueGame game, SignificantEvent source)
        {
            if (source.StoryId == null) return;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory story = director.Find(source.StoryId); if (story == null || story.Finished) return;
                string stage = null;
                if (source.Kind == "requested_food" || source.Kind == "supplies_requested" || source.Kind == "shelter_suggested") stage = "contacted";
                if (source.Kind == "supplies_acquired") stage = "returning";
                if (source.Kind == "shared_food") stage = story.Template == "group_supplies" ? "delivered" : "completed";
                if (source.Kind == "supplies_delivered" || source.Kind == "left_group" || source.Kind == "reunited" || source.Kind == "withdrew" || source.Kind == "confronted") stage = "completed";
                if (source.Kind == "shelter_reached" && story.Roles.TrueForAll(r => r.Status == NpcIntentStatus.Completed)) stage = "completed";
                if (source.Kind == "task_declined") stage = "failed";
                if (stage == null || stage == story.Stage) return;
                story.Stage = stage;
                if (story.Finished) director.End(story, stage, source.Turn);
                Session.Get.ResidentRecords.StoryChanged(source.Subject, story, source.Turn, source.Id);
                SocialGroup group = source.Subject == null ? null : source.Subject.SocialGroup;
                if (group != null && group.Plan != null && group.Plan.StoryId == story.Id)
                { group.Plan.Stage = stage; if (story.Finished) group.Plan.Destination = default(Location); }
            }
        }
        public static void GoalFinished(Actor owner, NpcIntent intent)
        {
            SocialGroup group = owner.SocialGroup;
            if (group != null && group.Plan != null && group.Plan.StoryId == intent.StoryId && intent.Status != NpcIntentStatus.Completed)
            { group.Plan.Stage = "failed"; group.Plan.Destination = default(Location); }
        }
        public static void Succession(RogueGame game, Actor leader)
        {
            if (!Session.Get.GamePreset.NpcPersonalitiesEnabled || leader.IsPlayer || leader.CountFollowers == 0) return;
            Actor best = null; int score = Int32.MinValue;
            foreach (Actor follower in leader.Followers)
            {
                if (!NpcIntentSystem.Enabled(follower) || follower.IsSleeping || follower.Location.Map != leader.Location.Map ||
                    !NpcKnowledgeSystem.Visible(game, follower, leader.Location) || PersonalitySystem.Bias(follower, DecisionKind.Group) < 0) continue;
                int value = PersonalitySystem.Bias(follower, DecisionKind.Group) + PersonalitySystem.Bias(follower, DecisionKind.Supplies) + game.Rules.ActorMaxFollowers(follower);
                if (best == null || value > score || (value == score && follower.PersonalityIdentity.CompareTo(best.PersonalityIdentity) < 0)) { best = follower; score = value; }
            }
            if (best == null) return;
            leader.TransferSocialGroupTo(best);
            PersonalitySystem.Report(game, new SignificantEvent("group_succession", best, leader, best.Location.Map, best.Location.Position,
                best.Location.Map.LocalTime.TurnCounter, otherIsDirect: false));
        }
    }
}
