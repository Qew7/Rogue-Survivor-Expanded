using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class SafetyModule
    {
        static void HearNeed(NpcReportContext c)
        {
            Actor owner = c.Listener; NpcFact fact = c.Fact; NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson other = fact.NamesOther ? knowledge.Person(fact.OtherId) : null;
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ThreatTurn ||
                (fact.EventTurn == other.ThreatTurn && fact.Confidence > other.ThreatConfidence)))
            { other.Danger = 100; other.ThreatTurn = fact.EventTurn; other.ThreatConfidence = fact.Confidence; other.ThreatCause = fact.EventId; }
            if (fact.Kind == "fled_in_fear" && other != null && fact.Confidence >= 40 &&
                (fact.EventTurn > other.ThreatTurn || fact.EventTurn == other.ThreatTurn && fact.Confidence > other.ThreatConfidence))
            { other.Danger = Math.Max(other.Danger, 70); other.ThreatTurn = fact.EventTurn;
                other.ThreatConfidence = fact.Confidence; other.ThreatCause = fact.EventId; knowledge.Revision++; }
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ViolationTurn ||
                (fact.EventTurn == other.ViolationTurn && fact.Confidence > other.ViolationConfidence)) && fact.EventId != other.AcknowledgedViolation)
            { other.Violation = 100; other.ViolationTurn = fact.EventTurn; other.ViolationConfidence = fact.Confidence; other.ViolationCause = fact.EventId; }
        }
    }
}
