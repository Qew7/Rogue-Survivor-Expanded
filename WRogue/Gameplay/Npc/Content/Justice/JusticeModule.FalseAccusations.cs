using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class JusticeModule
    {
        void RegisterFalseAccusations(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("theft_accusation", NpcRecordCategory.Encounters, false,
                e => (e.Subject ?? "Someone") + " accused " + (e.Other ?? "someone") + " of theft.",
                f => (f.ReportSubject ?? "Someone") + " claimed " + (f.ReportOther ?? "someone") +
                    " stole from " + (f.ClaimantName ?? "someone") + "'s " +
                    (f.Resource == "cache" ? "cache" : "base"))
                { SelfReportTone = NpcSelfReportTone.Harmful, ReportPriority = 3 });
            catalog.Event(new NpcEventDefinition("theft_accusation_denied", NpcRecordCategory.Encounters, false,
                e => (e.Subject ?? "Someone") + " denied " + (e.Other ?? "someone") + "'s theft accusation.",
                f => (f.ReportSubject ?? "Someone") + " denied " + (f.ReportOther ?? "someone") +
                    "'s theft accusation")
                { ReportDisputesKinds = new[] { "theft_accusation" }, ReportPriority = 5 });
            catalog.OnReport("theft_accusation", c => {
                NpcFact f = c.Fact;
                if (c.Improvement <= 0 || !f.NamesOther || f.OtherId == c.Listener.PersonalityIdentity) return;
                int strength = Math.Max(1, c.Improvement / 20);
                c.Listener.Personality.Opinion(f.OtherId, f.ReportOther).AdjustSocial(
                    trust: -strength, grievance: Math.Max(1, strength / 2));
            });
            catalog.OnReport("theft_accusation_denied", c => {
                if (c.Improvement <= 0 || !c.Fact.NamesSubject) return;
                c.Listener.Personality.Opinion(c.Fact.SubjectId, c.Fact.ReportSubject)
                    .AdjustSocial(trust: Math.Max(1, c.Improvement / 40));
            });
            catalog.AfterEvent("base_theft", CreateFalseAccusation);
            catalog.AfterEvent("cache_theft", CreateFalseAccusation);
        }

        static void CreateFalseAccusation(RogueGame game, SignificantEvent theft)
        {
            if (theft.Subject == null || theft.Map == null) return;
            foreach (Actor liar in theft.Map.Actors)
                CreateFalseAccusationFor(liar, theft);
        }

        static void CreateFalseAccusationFor(Actor liar, SignificantEvent theft)
        {
            if (liar.IsPlayer || liar.Personality == null || !liar.Personality.HasTrait("deceptive")) return;
            NpcFact source = liar.Personality.Knowledge.Facts.Find(f => f.EventId == theft.Id && f.Kind == theft.Kind);
            if (source == null || (source.Source != NpcKnowledgeSource.Participant &&
                source.Source != NpcKnowledgeSource.Witness) || !source.NamesSubject) return;
            NpcKnownPerson framed = null;
            int grievance = 19;
            foreach (NpcKnownPerson person in liar.Personality.Knowledge.People)
            {
                if (person.Id == liar.PersonalityIdentity || person.Id == theft.Subject.PersonalityIdentity ||
                    person.Id == source.OtherId || person.Id == source.ClaimantId ||
                    person.Dead || String.IsNullOrEmpty(person.Name)) continue;
                RelationshipRecord opinion = liar.Personality.Person(person.Id);
                int motive = opinion == null ? 0 : opinion.Grievance;
                if (person.Hostile) motive = Math.Max(motive, 30);
                if (motive <= grievance) continue;
                framed = person; grievance = motive;
            }
            if (framed == null) return;
            liar.Personality.Knowledge.Learn(new NpcFact {
                EventId = Session.Get.NextPersonalityEventId(), CauseId = theft.Id,
                Kind = "theft_accusation", EventTurn = theft.Turn, LearnedTurn = theft.Turn,
                SubjectId = liar.PersonalityIdentity, SubjectName = liar.UnmodifiedName,
                SubjectReportName = liar.UnmodifiedName,
                SubjectFactionId = liar.Faction == null ? (int?)null : liar.Faction.ID,
                OtherId = framed.Id, OtherName = framed.Name, OtherReportName = framed.Name,
                OtherFactionId = framed.FactionId < 0 ? (int?)null : framed.FactionId,
                ClaimantId = source.NamesOther ? source.OtherId : source.ClaimantId,
                ClaimantName = source.NamesOther ? source.OtherName : source.ClaimantName,
                Resource = theft.Kind == "cache_theft" ? "cache" : "base",
                Source = NpcKnowledgeSource.Participant, SourceId = liar.PersonalityIdentity,
                Confidence = 85, Place = source.Place, StoryId = source.StoryId });
        }
    }
}
