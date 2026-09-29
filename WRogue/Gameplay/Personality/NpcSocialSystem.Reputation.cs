using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcSocialSystem
    {
        public static void HearReport(Actor listener, NpcFact fact, int improvement)
        {
            if (fact.SubjectId == Guid.Empty || fact.SubjectId == listener.PersonalityIdentity) return;
            bool kept = fact.Kind == "promise_kept", broken = fact.Kind == "promise_broken";
            bool misconduct = fact.Kind == "base_theft" || fact.Kind == "contested_taken" || fact.Kind == "boundary_defied";
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
            if (fact.Kind == "requested_medicine")
            {
                NpcKnownPerson person = listener.Personality.Knowledge.Person(fact.SubjectId);
                if (person != null && (fact.EventTurn > person.MedicalTurn || fact.EventTurn == person.MedicalTurn && fact.Confidence > person.MedicalConfidence))
                { person.MedicalNeed = 100; person.MedicalConfidence = fact.Confidence; person.MedicalTurn = fact.EventTurn; person.MedicalCause = fact.EventId; person.MedicalStory = fact.StoryId; }
            }
        }
    }
}
