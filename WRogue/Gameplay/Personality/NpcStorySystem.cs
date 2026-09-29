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
        public static void EventFinished(RogueGame game, SignificantEvent source)
        {
            if (source.StoryId == null) return;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory story = director.Find(source.StoryId); if (story == null || story.Finished) return;
                string stage = null;
                if (source.Kind == "requested_food" || source.Kind == "supplies_requested" || source.Kind == "shelter_suggested") stage = "contacted";
                if (source.Kind == "supplies_acquired") stage = "acquired";
                if (source.Kind == "shared_food" && source.Other != null && story.Roles.Exists(r => r.ActorId == source.Subject.PersonalityIdentity &&
                    r.TargetId == source.Other.PersonalityIdentity && r.Goal == NpcIntentContent.Gather.Id)) stage = "delivered";
                if (source.Kind == "shared_food" && story.Template != "group_supplies" &&
                    story.Roles.TrueForAll(r => r.Status == NpcIntentStatus.Completed)) stage = "completed";
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
            NpcStory story = Session.Get.NpcDirector.Find(intent.StoryId);
            if (group != null && group.Plan != null && group.Plan.StoryId == intent.StoryId && story != null && story.Finished)
            { group.Plan.Stage = story.Stage; group.Plan.Destination = default(Location); }
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
