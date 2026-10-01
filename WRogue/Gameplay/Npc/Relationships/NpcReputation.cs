using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcReputation
    {
        internal static void Reputation(NpcReportContext c, bool kept, bool broken, bool misconduct)
        {
            Actor listener = c.Listener; NpcFact fact = c.Fact; int improvement = c.Improvement;
            if (fact.SubjectId == Guid.Empty || fact.SubjectId == listener.PersonalityIdentity) return;
            if (kept || broken || misconduct)
            {
                RelationshipRecord opinion = listener.Personality.Opinion(fact.SubjectId, fact.SubjectName);
                opinion.AdjustSocial(trust: kept ? improvement / 10 : -improvement / 10, grievance: broken || misconduct ? improvement / 15 : 0);
                opinion.Feeling = Math.Max(-100, Math.Min(100, opinion.Feeling + (kept ? improvement / 10 : -improvement / 10)));
                NpcKnownPerson person = listener.Personality.Knowledge.Person(fact.SubjectId);
                if (person != null)
                {
                    person.SocialCause = fact.EventId;
                    if (misconduct && fact.EventId != person.AcknowledgedViolation &&
                        (fact.EventTurn > person.ViolationTurn || fact.EventTurn == person.ViolationTurn && fact.Confidence > person.ViolationConfidence))
                    { person.Violation = 100; person.ViolationConfidence = fact.Confidence; person.ViolationTurn = fact.EventTurn; person.ViolationCause = fact.EventId; }
                }
            }
        }
    }
}
