using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcStorySystem
    {
        public static NpcGroupPlan ProposeGroupPlan(RogueGame game, Actor leader, IList<Actor> visible)
        {
            SocialGroup group = leader.SocialGroup; int turn = leader.Location.Map.LocalTime.TurnCounter;
            if (group == null || group.LeaderId != leader.PersonalityIdentity || group.Members.Count < 2 ||
                turn < group.NextPlanTurn || (group.Plan != null && !group.Plan.Finished)) return null;
            foreach (NpcIntent goal in leader.Personality.Intents)
                if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Coordinate.Id) return null;
            NpcKnowledge knowledge = leader.Personality.Knowledge;
            NpcFactionPolicy policy = game.NpcContent.FactionPolicy(leader.Faction.ID);
            foreach (NpcFact need in knowledge.Facts)
            {
                if (need.Kind != "requested_food" || turn - need.EventTurn > 60 || !group.Members.Contains(need.SubjectId)) continue;
                NpcKnownPerson beneficiary = knowledge.Person(need.SubjectId); if (beneficiary == null || beneficiary.Dead) continue;
                if (PersonalitySystem.Bias(leader, DecisionKind.Group) + PersonalitySystem.Bias(leader, DecisionKind.Compassion) + policy.Supply < 15) continue;
                NpcKnownPlace cache = knowledge.Places.Find(p => p.Kind == "food" && p.Units >= 2 && turn - p.SeenTurn < 180);
                if (cache == null) continue;
                Actor collector = null; int score = Int32.MinValue;
                foreach (Actor member in visible)
                {
                    if (member.IsPlayer || member.IsSleeping || member.PersonalityIdentity == need.SubjectId || member.SocialGroup != group || !NpcIntentSystem.Enabled(member)) continue;
                    int value = NpcIntentContent.Gather.ScoreKnown(member, 0, false);
                    if (value >= NpcIntentContent.Gather.Threshold && value > score) { collector = member; score = value; }
                }
                if (collector == null) continue;
                if (!Session.Get.NpcDirector.CanOpen(leader.Location.Map, turn, "group:" + group.Identity, cache.Place, collector.PersonalityIdentity)) return null;
                return new NpcGroupPlan { Kind = "group_supplies", Stage = "proposed", CauseId = need.EventId,
                    CollectorId = collector.PersonalityIdentity, BeneficiaryId = need.SubjectId, Destination = cache.Place,
                    Deadline = turn + WorldTime.TURNS_PER_DAY };
            }
            if (PersonalitySystem.Bias(leader, DecisionKind.Group) + policy.Shelter < 15) return null;
            NpcFact threat = knowledge.Facts.Find(f => (f.Kind == "attack" || f.Kind == "murder") && turn - f.EventTurn < 180);
            if (threat == null || leader.Location.Map.GetTileAt(leader.Location.Position).IsInside) return null;
            NpcKnownPlace shelter = knowledge.Places.Find(p => p.Kind == "shelter" && p.Place != leader.Location);
            if (shelter == null || !Session.Get.NpcDirector.CanOpen(leader.Location.Map, turn, "group:" + group.Identity, shelter.Place, leader.PersonalityIdentity)) return null;
            return new NpcGroupPlan { Kind = "group_shelter", Stage = "proposed", CollectorId = leader.PersonalityIdentity,
                BeneficiaryId = leader.PersonalityIdentity, Destination = shelter.Place, CauseId = threat.EventId, Deadline = turn + 180 };
        }
        public static void AcceptTask(RogueGame game, Actor owner, SignificantEvent source)
        {
            NpcGroupPlan plan = source.Task;
            if (owner.SocialGroup == null || source.Subject.SocialGroup != owner.SocialGroup || plan.CollectorId != owner.PersonalityIdentity) return;
            NpcKnownPerson beneficiary = source.Subject.Personality.Knowledge.Person(plan.BeneficiaryId);
            // The leader actually told the collector the beneficiary and observed destination.
            if (beneficiary == null) return;
            NpcKnownPlace cache = source.Subject.Personality.Knowledge.Places.Find(p => p.Kind == "food" && p.Place == plan.Destination);
            if (cache != null) owner.Personality.Knowledge.RememberPlace(new NpcKnownPlace(cache.Place, cache.Kind, cache.SeenTurn, cache.Units, cache.Risk));
            NpcKnownPerson target = new NpcKnownPerson { Id = beneficiary.Id, Name = beneficiary.Name, Place = beneficiary.Place, SeenTurn = beneficiary.SeenTurn };
            NpcIntent goal = StartKnown(owner, target, NpcIntentContent.Gather, source.Id, plan.StoryId, plan.Destination, owner.SocialGroup.Identity);
            if (goal != null) { goal.CoordinatorId = source.Subject.PersonalityIdentity; goal.CoordinatorPlace = source.Subject.Location; }
            if (goal == null && owner.Personality.Reactions.Count < 4)
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I won't take that task.", source.Id, source.Turn, "task_declined", plan.StoryId));
            else if (goal != null) plan.Stage = "fetching";
        }
        public static void AcceptShelter(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (!NpcIntentSystem.Enabled(owner)) return;
            NpcIntent goal = StartKnown(owner, new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location },
                NpcIntentContent.Shelter, source.Id, source.Task.StoryId, source.Task.Destination, owner.SocialGroup.Identity);
            if (goal == null && owner != source.Subject && owner.Personality.Reactions.Count < 4)
                owner.Personality.Reactions.Add(new NpcReaction(source.Subject, "I'm not coming to that shelter.", source.Id,
                    source.Turn, "shelter_declined", source.StoryId));
        }
    }
}
