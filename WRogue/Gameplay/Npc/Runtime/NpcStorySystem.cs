using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcStorySystem
    {
        public static NpcIntent StartKnown(NpcContentCatalog catalog, Actor owner, NpcKnownPerson target,
            NpcIntentDefinition definition, long cause = 0, string storyId = null,
            Location destination = default(Location), Guid groupId = default(Guid))
        { return NpcGoalLifecycle.Start(catalog, owner, target, definition, cause, storyId,
            destination: destination, groupId: groupId); }
        public static void EventFinished(RogueGame game, SignificantEvent source)
        {
            if (source.StoryId == null) return;
            NpcStoryDirector director = Session.Get.NpcDirector;
            lock (director)
            {
                NpcStory story = director.Find(source.StoryId); if (story == null || story.Finished) return;
                NpcEventDefinition definition = game.NpcContent.Event(source.Kind);
                string stage = definition == null || definition.StoryStage == null ? null : definition.StoryStage(game, story, source);
                if (stage == null || stage == story.Stage) return;
                story.Stage = stage;
                if (story.Finished) director.End(story, stage, source.Turn);
                Session.Get.ResidentRecords.StoryChanged(source.Subject, story, source.Turn, source.Id);
                SocialGroup group = source.Subject == null ? null : source.Subject.SocialGroup;
                NpcGroupPlan faction = source.Other != null && source.Other.Personality != null && source.Other.Personality.FactionPlan != null && source.Other.Personality.FactionPlan.StoryId == story.Id ?
                    source.Other.Personality.FactionPlan : source.Subject != null && source.Subject.Personality != null ? source.Subject.Personality.FactionPlan : null;
                if (faction != null && faction.StoryId == story.Id)
                { faction.Stage = stage; if (story.Finished) faction.Destination = default(Location); }
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
            if (best.CountFollowers == 0) return;
            var dissenters = new List<Actor>();
            foreach (Actor follower in new List<Actor>(best.Followers))
            {
                if (!NpcIntentSystem.Enabled(follower) || follower.IsSleeping || follower.Location.Map != best.Location.Map ||
                    !NpcKnowledgeSystem.Visible(game, follower, best.Location)) continue;
                RelationshipRecord opinion = follower.Personality.Person(best.PersonalityIdentity);
                int grievance = opinion == null ? 0 : opinion.Grievance;
                int feeling = opinion == null ? 0 : opinion.Feeling;
                if (follower.TrustInLeader < -60 || grievance >= 40 ||
                    PersonalitySystem.Bias(follower, DecisionKind.Group) < -15 && feeling < 0)
                    dissenters.Add(follower);
            }
            if (dissenters.Count == 0) return;
            Actor rival = null; int rivalScore = Int32.MinValue;
            foreach (Actor dissenter in dissenters)
            {
                int value = game.Rules.ActorMaxFollowers(dissenter) + PersonalitySystem.Bias(dissenter, DecisionKind.Group);
                if (rival == null || value > rivalScore || value == rivalScore && dissenter.PersonalityIdentity.CompareTo(rival.PersonalityIdentity) < 0)
                { rival = dissenter; rivalScore = value; }
            }
            foreach (Actor dissenter in dissenters) best.RemoveFollower(dissenter);
            rival.InitializeSocialGroup();
            foreach (Actor dissenter in dissenters) if (dissenter != rival) rival.AddFollower(dissenter);
            foreach (Actor dissenter in dissenters)
                PersonalitySystem.Report(game, new SignificantEvent("group_split", dissenter, best,
                    dissenter.Location.Map, dissenter.Location.Position, dissenter.Location.Map.LocalTime.TurnCounter));
        }
    }
}
