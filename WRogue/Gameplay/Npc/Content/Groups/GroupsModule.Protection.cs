using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class GroupsModule
    {
        void RegisterProtection(NpcCatalogBuilder c)
        {
            c.Event(new NpcEventDefinition("group_protection_requested", NpcRecordCategory.Combat | NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to protect a companion."));
            c.Collective(new NpcCollectiveDefinition("group_protection", "group_protection_requested", ProtectionOffer,
                (a, b, p) => b.PersonalityIdentity == p.CollectorId && NpcIntentSystem.Enabled(b),
                (a, p) => "Our companion was attacked. Keep the attacker away from us.", AcceptProtection));
            c.On("group_protection_requested", NpcObservationPhase.Knowledge, o => NpcStorySystem.AcceptCollective(o.Game, o.Owner, o.Source));
            c.AfterEvent("defended_person", (game, e) => {
                SocialGroup group = e.Subject == null ? null : e.Subject.SocialGroup;
                NpcGroupPlan plan = group == null ? null : group.Plan;
                if (plan == null || plan.Kind != "group_protection" || plan.Finished || plan.CollectorId != e.Subject.PersonalityIdentity) return;
                NpcStory story = Session.Get.NpcDirector.Find(plan.StoryId);
                if (story == null || story.Finished) return;
                NpcFact incident = e.Subject.Personality.Knowledge.Facts.Find(f => f.EventId == plan.CauseId && f.Kind == "attack");
                if (incident == null || e.Other == null || e.Other.PersonalityIdentity != incident.OtherId) return;
                Session.Get.NpcDirector.End(story, "completed", e.Turn); plan.Stage = "completed"; plan.Destination = default(Location);
                Session.Get.ResidentRecords.StoryChanged(e.Subject, story, e.Turn, e.Id);
            });
        }
        static NpcCollectiveOffer ProtectionOffer(NpcCollectiveContext c)
        {
            int priority = PersonalitySystem.Bias(c.Leader, DecisionKind.Group) + c.Policy.Security;
            if (priority < 15) return null;
            foreach (NpcFact fact in c.Knowledge.Facts)
            {
                if (fact.Kind != "attack" || c.Turn - fact.EventTurn >= 180 || !c.Group.Members.Contains(fact.SubjectId)) continue;
                NpcKnownPerson threat = c.Knowledge.Person(fact.OtherId);
                if (threat == null || threat.Dead || c.Group.Members.Contains(threat.Id)) continue;
                foreach (Actor member in c.Visible)
                {
                    if (member.IsPlayer || member.IsSleeping || member == c.Leader || member.SocialGroup != c.Group ||
                        member.PersonalityIdentity == fact.SubjectId || !NpcIntentSystem.Enabled(member)) continue;
                    NpcIntentDefinition capability = c.Game.NpcContent.Capability("defend_person");
                    if (capability.AssignedScore(c.Game.NpcContent, member, threat.Id) < capability.Threshold) continue;
                    return new NpcCollectiveOffer(new NpcGroupPlan { Kind = "group_protection", Stage = "proposed", CauseId = fact.EventId,
                        CollectorId = member.PersonalityIdentity, BeneficiaryId = fact.SubjectId, Destination = threat.Place, Deadline = c.Turn + 180 }, priority);
                }
            }
            return null;
        }
        static void AcceptProtection(RogueGame game, Actor owner, SignificantEvent source)
        {
            if (owner.PersonalityIdentity != source.Task.CollectorId) return;
            NpcFact evidence = source.Subject.Personality.Knowledge.Facts.Find(f => f.Kind == "attack" && f.EventId == source.Task.CauseId);
            NpcKnownPerson threat = evidence == null ? null : source.Subject.Personality.Knowledge.Person(evidence.OtherId);
            if (threat == null) return;
            owner.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = threat.Id, Name = threat.Name, Place = threat.Place,
                SeenTurn = threat.SeenTurn, Confidence = 80, Source = NpcKnowledgeSource.Told, FactionId = threat.FactionId, GroupId = threat.GroupId });
            NpcIntent existing = null;
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.DefinitionId == "defend_person" && intent.TargetId == threat.Id) { existing = intent; break; }
            if (existing != null)
            {
                NpcStory story = Session.Get.NpcDirector.Find(source.StoryId);
                if (story != null) Session.Get.NpcDirector.Link(story, existing.StoryId, owner, source.Id);
                source.Task.Stage = "responding"; return;
            }
            NpcIntent goal = NpcStorySystem.StartKnown(owner, threat, game.NpcContent.Capability("defend_person"), source.Id,
                source.StoryId, groupId: owner.SocialGroup.Identity, catalog: game.NpcContent);
            if (goal != null) source.Task.Stage = "responding";
            else if (owner.Personality.Reactions.Count < 4) owner.Personality.Reactions.Add(new NpcReaction(source.Subject,
                "I won't fight for that task.", source.Id, source.Turn, "task_declined", source.StoryId));
        }
    }
}
