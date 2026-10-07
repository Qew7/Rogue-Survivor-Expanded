using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcReputation
    {
        internal static void Reputation(NpcReportContext c, bool kept, bool broken, bool misconduct)
        {
            if (kept || broken || misconduct) React(c, false, kept, misconduct ? 2 : broken ? 1 : 0);
        }
        internal static void Help(NpcReportContext c, bool helperIsOther = false)
        { React(c, helperIsOther, true, 0); }
        internal static void Attack(NpcReportContext c)
        { React(c, true, false, 3); }
        internal static void PermissionClaim(NpcReportContext c)
        {
            if (c.Improvement <= 0 || !c.Fact.NamesSubject) return;
            int strength = Math.Max(1, c.Improvement / 15);
            RelationshipRecord opinion = c.Listener.Personality.Opinion(c.Fact.SubjectId, c.Fact.ReportSubject);
            opinion.AdjustSocial(trust: strength, grievance: -strength);
            opinion.Feeling = Math.Min(100, opinion.Feeling + strength);
        }
        internal static void StolenGoods(Actor listener, NpcFact fact, int improvement)
        {
            if (improvement <= 0 || !fact.NamesOther || listener.Personality == null) return;
            RelationshipRecord victim = listener.Personality.Person(fact.OtherId);
            bool ownGroup = listener.PersonalityIdentity == fact.OtherId ||
                listener.SocialGroup != null && listener.SocialGroup.Identity == fact.ClaimantGroupId;
            if (!ownGroup && victim == null) return;
            int law = PersonalitySystem.Bias(listener, DecisionKind.Law);
            int compassion = PersonalitySystem.Bias(listener, DecisionKind.Compassion);
            if (fact.Kind == "stolen_goods_found")
            {
                if (victim != null && compassion > 0) victim.AdjustSocial(attachment: Math.Max(1, improvement / 40));
                return;
            }
            if (!fact.NamesSubject || fact.SubjectId == listener.PersonalityIdentity) return;
            int loyalty = ownGroup ? 30 : victim == null ? 0 : victim.Feeling / 2 + victim.Attachment / 2;
            int judgment = law + compassion / 2 + loyalty;
            if (judgment == 0) return;
            int change = Math.Max(1, improvement / 20) * (judgment > 0 ? -1 : 1);
            RelationshipRecord holder = listener.Personality.Opinion(fact.SubjectId, fact.ReportSubject);
            holder.Feeling = Math.Max(-100, Math.Min(100, holder.Feeling + change));
            holder.AdjustSocial(trust: change > 0 ? change : -Math.Min(holder.Trust, -change),
                grievance: change < 0 ? -change : 0);
        }

        // Severity: 0 is help, 1 is a broken promise, 2 is misconduct, 3 is violence.
        static void React(NpcReportContext c, bool other, bool positive, int severity)
        {
            NpcFact fact = c.Fact;
            Actor listener = c.Listener;
            Guid id = other ? fact.OtherId : fact.SubjectId;
            string reportedName = other ? fact.ReportOther : fact.ReportSubject;
            int? factionId = other ? fact.OtherFactionId : fact.SubjectFactionId;
            if (id == Guid.Empty || id == listener.PersonalityIdentity || c.Improvement <= 0 ||
                String.IsNullOrEmpty(reportedName)) return;

            int compassion = PersonalitySystem.Bias(listener, DecisionKind.Compassion);
            int law = PersonalitySystem.Bias(listener, DecisionKind.Law);
            int trade = PersonalitySystem.Bias(listener, DecisionKind.Trade);
            int courage = PersonalitySystem.Bias(listener, DecisionKind.Courage);
            int response = positive ? 100 + compassion * 2 + trade / 2 :
                severity == 1 ? 100 + law + compassion / 2 : 100 + law * 2 + compassion / 2;
            response = Math.Max(25, Math.Min(180, response));
            int strength = c.Improvement * response / 1000;
            if (severity == 3) strength = strength * 3 / 2;
            if (strength == 0) return;

            // A faction description gives no grounds to judge a named stranger.
            bool named = other ? fact.NamesOther : fact.NamesSubject;
            RelationshipRecord opinion;
            if (named)
                opinion = listener.Personality.Opinion(id, reportedName);
            else if (factionId.HasValue)
            {
                opinion = listener.Personality.OpinionFaction(factionId.Value, Models.Factions[factionId.Value].Name);
                strength = Math.Max(1, strength / 2);
            }
            else return;

            opinion.AdjustSocial(trust: positive ? strength : -strength,
                grievance: positive ? 0 : Math.Max(1, strength / 2),
                fear: severity == 3 ? Math.Max(1, strength * Math.Max(0, 25 - courage) / 25) : 0);
            opinion.Feeling = Math.Max(-100, Math.Min(100, opinion.Feeling + (positive ? strength : -strength)));
            if (!named) return;
            NpcKnownPerson person = listener.Personality.Knowledge.Person(id);
            if (person != null)
            {
                person.SocialCause = fact.EventId;
                if (severity >= 2 && fact.EventId != person.AcknowledgedViolation &&
                    (fact.EventTurn > person.ViolationTurn || fact.EventTurn == person.ViolationTurn && fact.Confidence > person.ViolationConfidence))
                { person.Violation = 100; person.ViolationConfidence = fact.Confidence; person.ViolationTurn = fact.EventTurn; person.ViolationCause = fact.EventId; }
            }
        }
    }
    static class NpcTestimony
    {
        public static void Refute(NpcContentCatalog catalog, Actor listener, Actor speaker, NpcFact claim)
        {
            if (listener.Personality.Knowledge.Facts.Exists(f => f.EventId == claim.EventId && f.Kind == "false_testimony_exposed")) return;
            if (!claim.NamesSubject) return;
            int turn = listener.Location.Map.LocalTime.TurnCounter;
            Guid claimant = claim.SubjectId;
            string claimantName = claim.SubjectName;
            listener.Personality.Opinion(claimant, claimantName).AdjustSocial(trust: -15, grievance: 10);
            long id = Session.Get.NextPersonalityEventId();
            var observed = new ObservedEvent("false_testimony_exposed", turn, claimantName, listener.UnmodifiedName,
                true, subjectId: claimant, otherId: listener.PersonalityIdentity, eventId: id, causeId: claim.EventId);
            NpcRecordDescriptions.Annotate(observed, catalog.Event("false_testimony_exposed"), catalog);
            listener.Personality.Remember(observed);
            Session.Get.ResidentRecords.Observe(listener, observed);
            listener.Personality.Knowledge.Learn(new NpcFact { EventId = claim.EventId, Kind = "false_testimony_exposed",
                SubjectId = claimant, SubjectName = claimantName, EventTurn = turn, LearnedTurn = turn,
                Source = NpcKnowledgeSource.Inferred, SourceId = listener.PersonalityIdentity, Confidence = 100,
                Place = listener.Location, NoSubjectLocation = true });
            MemoryDefinition definition = catalog.Personalities.Memory("false_testimony_exposed");
            if (definition != null) NpcMemoryProcessor.Add(listener, definition, turn, claimantName, null,
                subjectId: claimant, relations: speaker.PersonalityIdentity == claimant ?
                    NpcMemoryRelations.Observed(speaker, null) : new NpcMemoryRelations { Person = claimant, PersonName = claimantName }, impact: -8);
        }
    }
}
