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
            NpcKnownPerson other = knowledge.Person(fact.OtherId);
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ThreatTurn ||
                (fact.EventTurn == other.ThreatTurn && fact.Confidence > other.ThreatConfidence)))
            { other.Danger = 100; other.ThreatTurn = fact.EventTurn; other.ThreatConfidence = fact.Confidence; other.ThreatCause = fact.EventId; }
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ViolationTurn ||
                (fact.EventTurn == other.ViolationTurn && fact.Confidence > other.ViolationConfidence)) && fact.EventId != other.AcknowledgedViolation)
            { other.Violation = 100; other.ViolationTurn = fact.EventTurn; other.ViolationConfidence = fact.Confidence; other.ViolationCause = fact.EventId; }
        }
    }
}
