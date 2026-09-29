using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ConflictResolutionModule
    {
        void RegisterEvents(NpcCatalogBuilder c)
        {
            Event(c, "threatened", " threatened ", NpcRecordCategory.Combat);
            Event(c, "threat_accepted", " agreed to keep away from ", NpcRecordCategory.Combat);
            Event(c, "threat_defied", " refused to yield to ", NpcRecordCategory.Combat);
            Event(c, "apologized", " apologized to ", NpcRecordCategory.Help);
            Event(c, "apology_accepted", " accepted an apology from ", NpcRecordCategory.Help);
            Event(c, "apology_refused", " refused an apology from ", NpcRecordCategory.Help);
            Event(c, "member_expelled", " expelled a companion: ", NpcRecordCategory.Encounters);
            Event(c, "retaliated", " struck back at ", NpcRecordCategory.Combat);
            Event(c, "defended_person", " intervened against ", NpcRecordCategory.Combat);
            NpcMemoryContent.Received(c, "was_threatened", "Someone threatened me", "threatened", -8, "mistrustful");
            NpcMemoryContent.Received(c, "accepted_apology", "Accepted an apology", "apology_accepted", 5, null);
            NpcMemoryContent.Received(c, "was_expelled", "Was expelled from a group", "member_expelled", -15, "hermit");
            c.Memory(new MemoryDefinition("stood_for_companion", "Acted to defend a companion", 2, 5,
                new[] { new MemoryTrigger("defended_person", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP)), false);
            c.On("threatened", NpcObservationPhase.Knowledge, o => {
                if (o.Owner != o.Source.Other) return;
                NpcKnownPerson person = o.Owner.Personality.Knowledge.Person(o.Source.Subject.PersonalityIdentity);
                if (person == null) return;
                person.Danger = 100; person.ThreatTurn = o.Source.Turn; person.ThreatConfidence = 100; person.ThreatCause = o.Source.Id;
                o.Owner.Personality.Opinion(person.Id, person.Name).AdjustSocial(fear: 15); o.Owner.Personality.Knowledge.Revision++;
            });
            c.On("threatened", NpcObservationPhase.Replies, o => {
                if (o.Owner != o.Source.Other) return;
                bool accept = PersonalitySystem.Bias(o.Owner, DecisionKind.Courage) < 0;
                NpcReplies.Reply(o.Owner, o.Source.Subject, o.Source, accept ? "threat_accepted" : "threat_defied",
                    "I'll keep my distance.", "You can't scare me.");
            });
            c.On("apologized", NpcObservationPhase.Replies, o => {
                if (o.Owner != o.Source.Other) return;
                RelationshipRecord opinion = o.Owner.Personality.Person(o.Source.Subject.PersonalityIdentity);
                int hurt = opinion == null ? 0 : opinion.Grievance + opinion.Fear / 2;
                bool accept = 20 + PersonalitySystem.Bias(o.Owner, DecisionKind.Compassion) + PersonalitySystem.Bias(o.Owner, DecisionKind.Law) - hurt >= 0;
                NpcReplies.Reply(o.Owner, o.Source.Subject, o.Source, accept ? "apology_accepted" : "apology_refused",
                    "We can try again. You'll have to earn my trust.", "I'm not ready to forgive you.");
            });
            c.On("apology_accepted", NpcObservationPhase.Relationships, o => {
                if (!o.Direct || o.Source.Subject == null || o.Source.Other == null) return;
                Actor other = o.Owner == o.Source.Subject ? o.Source.Other : o.Owner == o.Source.Other ? o.Source.Subject : null;
                if (other == null) return;
                o.Owner.Personality.Opinion(other.PersonalityIdentity, other.UnmodifiedName).AdjustSocial(trust: 8, grievance: -10, fear: -5);
            });
            c.On("threat_accepted", NpcObservationPhase.Relationships, o => {
                if (o.Owner != o.Source.Other) return;
                NpcKnownPerson person = o.Owner.Personality.Knowledge.Person(o.Source.Subject.PersonalityIdentity);
                if (person != null) { person.Violation = 0; o.Owner.Personality.Knowledge.Revision++; }
            });
        }
        static void Event(NpcCatalogBuilder c, string id, string text, NpcRecordCategory category)
        { c.Event(new NpcEventDefinition(id, category, true, e => (e.Subject ?? "Someone") + text + (e.Other ?? "someone") + ".")); }
    }
}
