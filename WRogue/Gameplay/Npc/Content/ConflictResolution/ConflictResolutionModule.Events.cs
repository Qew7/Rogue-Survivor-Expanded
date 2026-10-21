using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ConflictResolutionModule
    {
        void RegisterEvents(NpcCatalogBuilder c)
        {
            Event(c, "threatened", " threatened ", NpcRecordCategory.Combat, NpcSelfReportTone.Harmful);
            Event(c, "threat_accepted", " agreed to keep away from ", NpcRecordCategory.Combat);
            Event(c, "threat_defied", " refused to yield to ", NpcRecordCategory.Combat);
            Event(c, "apologized", " apologized to ", NpcRecordCategory.Help, NpcSelfReportTone.Helpful);
            Event(c, "apology_accepted", " accepted an apology from ", NpcRecordCategory.Help);
            Event(c, "apology_refused", " refused an apology from ", NpcRecordCategory.Help);
            Event(c, "member_expelled", " expelled a companion: ", NpcRecordCategory.Encounters, NpcSelfReportTone.Harmful);
            Event(c, "retaliated", " struck back at ", NpcRecordCategory.Combat, NpcSelfReportTone.Harmful);
            Event(c, "defended_person", " intervened against ", NpcRecordCategory.Combat, NpcSelfReportTone.Helpful);
            c.Event(new NpcEventDefinition("pursuit_announced", NpcRecordCategory.Combat | NpcRecordCategory.Intentions, true,
                e => (e.Subject ?? "A police officer") + " announced an active pursuit.",
                f => (f.ReportSubject ?? "A police officer") + " announced a pursuit of " + (f.ReportOther ?? "someone"),
                NpcEventFields.Subject | NpcEventFields.Resource, isPrivate: true)
            { PrivateAudience = e => e.Subject, ReportActorRole = NpcReportActorRole.Subject,
              DescribeSelfReport = (f, speaker) => "I'm pursuing " + (f.ReportOther ?? "someone") });
            c.OnReport("pursuit_announced", HearPursuit);
            c.Event(new NpcEventDefinition("pursuit_response_announced", NpcRecordCategory.Combat | NpcRecordCategory.Intentions, true,
                e => (e.Subject ?? "Someone") + " said how they would respond to a pursuit.",
                f => (f.ReportSubject ?? "Someone") + (f.Resource == "retaliate" ? " said they would fight back against " :
                    " said they would hide from ") + (f.ReportOther ?? "their pursuer"),
                NpcEventFields.Subject | NpcEventFields.Resource, isPrivate: true)
            { PrivateAudience = e => e.Subject, ReportActorRole = NpcReportActorRole.Subject,
              DescribeSelfReport = (f, speaker) => f.Resource == "retaliate" ?
                  "I will fight back against " + (f.ReportOther ?? "my pursuer") :
                  "I will hide from " + (f.ReportOther ?? "my pursuer") });
            c.Event(new NpcEventDefinition("patience_lost", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " snapped at " + (e.Other ?? "someone") + ".",
                f => (f.ReportSubject ?? "Someone") + " snapped at " + (f.ReportOther ?? "someone"))
                { ReportPriority = 3 });
            c.Event(new NpcEventDefinition("patience_escalated", NpcRecordCategory.Combat, true,
                e => (e.Subject ?? "Someone") + " struck at " + (e.Other ?? "someone") + " after losing patience.",
                f => (f.ReportSubject ?? "Someone") + " struck at " + (f.ReportOther ?? "someone") + " after losing patience")
                { SelfReportTone = NpcSelfReportTone.Harmful, ReportPriority = 8 });
            c.Memory(new MemoryDefinition("lost_patience", "I lost my patience with someone", 2, 2,
                new[] { new MemoryTrigger("patience_lost", (a, e) => a == e.Subject),
                        new MemoryTrigger("patience_escalated", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "hotheaded", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Other, -5).ChangeDisposition(NpcPersonalityAxis.Patience, -6), false);
            c.Memory(new MemoryDefinition("received_outburst", "Someone lost patience with me", 2, 2,
                new[] { new MemoryTrigger("patience_lost", (a, e) => a == e.Other),
                        new MemoryTrigger("patience_escalated", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -8).ChangeDisposition(NpcPersonalityAxis.Patience, -5), false);
            c.Memory(new MemoryDefinition("witnessed_outburst", "I saw someone lose their patience", 2, 2,
                new[] { new MemoryTrigger("patience_lost", (a, e) => a != e.Subject && a != e.Other),
                        new MemoryTrigger("patience_escalated", (a, e) => a != e.Subject && a != e.Other) },
                new MemoryOutcome(null, "vigilant", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, 0).ChangeDisposition(NpcPersonalityAxis.Patience, -2), false);
            c.On("request_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 16, "I only asked for help!"));
            c.On("trade_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 12, "Forget the trade, then!"));
            c.On("resource_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 18, "I needed those supplies too!"));
            c.On("contested_taken", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 30, "Those supplies were mine!"));
            c.On("restitution_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 22, "You owe me for that!"));
            c.On("boundary_defied", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 20, "You ignored my warning!"));
            c.On("shelter_care_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 15, "I was offering you a way out!"));
            c.On("shelter_care_failed", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 30, "You broke our agreement!"));
            c.On("apology_refused", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 12, "I tried to make this right!"));
            c.On("member_expelled", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 25, "You're throwing me out?"));
            c.On("abandoned", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 25, "You left me behind!", victimIsSubject: true));
            c.On("threat_defied", NpcObservationPhase.Replies, o => NpcReplies.Setback(o, 20, "I said stay away!"));
            c.On("traded", NpcObservationPhase.Relationships, o => Cool(o));
            c.On("apology_accepted", NpcObservationPhase.Relationships, o => Cool(o));
            c.On("resource_yielded", NpcObservationPhase.Relationships, o => Cool(o));
            c.On("shelter_care_completed", NpcObservationPhase.Relationships, o => Cool(o));
            NpcMemoryContent.Received(c, "was_threatened", "Someone threatened me", "threatened", -8, "mistrustful");
            NpcMemoryContent.Received(c, "accepted_apology", "Accepted an apology", "apology_accepted", 5, null);
            NpcMemoryContent.Received(c, "was_expelled", "Was expelled from a group", "member_expelled", -15, "hermit");
            c.Memory(new MemoryDefinition("stood_for_companion", "Acted to defend a companion", 2, 2,
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
                bool accept = PersonalitySystem.Bias(o.Owner, NpcPersonalityAxis.Courage) < 0;
                NpcReplies.Reply(o.Owner, o.Source.Subject, o.Source, accept ? "threat_accepted" : "threat_defied",
                    accept ? "I'll keep my distance." : "You can't scare me.");
            });
            c.On("apologized", NpcObservationPhase.Replies, o => {
                if (o.Owner != o.Source.Other) return;
                RelationshipRecord opinion = o.Owner.Personality.Person(o.Source.Subject.PersonalityIdentity);
                int hurt = opinion == null ? 0 : opinion.Grievance + opinion.Fear / 2;
                bool accept = 20 + PersonalitySystem.Bias(o.Owner, NpcPersonalityAxis.Compassion) + PersonalitySystem.Bias(o.Owner, NpcPersonalityAxis.Law) +
                    PersonalitySystem.Disposition(o.Owner, NpcPersonalityAxis.Patience, o.Game.NpcContent) / 2 - hurt >= 0;
                NpcReplies.Reply(o.Owner, o.Source.Subject, o.Source, accept ? "apology_accepted" : "apology_refused",
                    accept ? "We can try again. You'll have to earn my trust." : "I'm not ready to forgive you.");
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
        static void Event(NpcCatalogBuilder c, string id, string text, NpcRecordCategory category,
            NpcSelfReportTone tone = NpcSelfReportTone.Neutral)
        { c.Event(new NpcEventDefinition(id, category, true, e => (e.Subject ?? "Someone") + text + (e.Other ?? "someone") + ".") { SelfReportTone = tone }); }
        static void Cool(NpcObservation observation)
        {
            if (!observation.Owner.IsPlayer && (observation.Owner == observation.Source.Subject ||
                observation.Owner == observation.Source.Other))
                observation.Owner.Personality.ChangeFrustration(observation.Source.Turn, -20);
        }
    }
}
