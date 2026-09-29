using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcKnowledgeSystem
    {
        static void RememberMedicine(Actor actor, NpcKnowledge knowledge, Inventory items, Location place, int turn, int risk)
        {
            int units = 0;
            foreach (Item item in items.Items)
                if (item is Engine.Items.ItemMedicine && ((Engine.Items.ItemMedicine)item).Healing > 0) units += item.Quantity;
            NpcKnownPlace old = knowledge.Places.Find(p => p.Kind == "medicine" && p.Place == place);
            if (units > 0 && (old == null || old.Units != units || turn - old.SeenTurn > 180))
                knowledge.Learn(new NpcFact { Kind = "medicine_cache", EventId = Session.Get.NextPersonalityEventId(),
                    EventTurn = turn, LearnedTurn = turn, Confidence = 90, Source = NpcKnowledgeSource.Witness,
                    SourceId = actor.PersonalityIdentity, Place = place, Units = units, Risk = risk });
            if (old != null || units > 0) knowledge.RememberPlace(new NpcKnownPlace(place, "medicine", turn, units, risk));
        }
        // Observation adapters describe what happened to state, without choosing a goal.
        static void Conditions(Actor owner, SignificantEvent source, bool seesSubject, bool seesOther, bool direct)
        {
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson subject = seesSubject ? knowledge.Person(source.Subject.PersonalityIdentity) : null;
            NpcKnownPerson other = seesOther ? knowledge.Person(source.Other.PersonalityIdentity) : null;
            int confidence = direct ? 100 : 90;
            if (source.Kind == "requested_food" && subject != null)
            {
                subject.FoodNeed = 100; subject.FoodNeedTurn = source.Turn; subject.FoodConfidence = confidence;
                subject.NeedCause = source.Id; subject.NeedStory = source.StoryId; knowledge.Revision++;
            }
            if (source.Kind == "shared_food" && other != null)
            { other.FoodNeed = 0; other.FoodNeedTurn = source.Turn; knowledge.Revision++; }
            if ((source.Kind == "attack" || source.Kind == "murder") && other != null && other.Id != owner.PersonalityIdentity)
            {
                other.Danger = 100; other.Violation = 100; other.ThreatTurn = source.Turn;
                other.ThreatConfidence = confidence; other.ThreatCause = source.Id; knowledge.Revision++;
                other.ViolationTurn = source.Turn; other.ViolationConfidence = confidence; other.ViolationCause = source.Id;
            }
            if (source.Kind == "base_theft" && subject != null && subject.Id != owner.PersonalityIdentity)
            {
                subject.Violation = 100; subject.ThreatTurn = source.Turn; subject.ThreatConfidence = confidence;
                subject.ThreatCause = source.Id; knowledge.Revision++;
                subject.ViolationTurn = source.Turn; subject.ViolationConfidence = confidence; subject.ViolationCause = source.Id;
            }
            if (source.Kind == "helped" && source.Subject == owner && other != null)
            { other.SocialCause = source.Id; other.ReciprocityTurn = source.StoryId == null ? source.Turn : source.Turn + WorldTime.TURNS_PER_DAY; }
            if (source.Kind == "confronted" && source.Subject == owner && other != null)
            { other.Violation = 0; other.AcknowledgedViolation = source.CauseId; knowledge.Revision++; }
        }
        static void ReportConditions(Actor owner, NpcFact fact)
        {
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson subject = knowledge.Person(fact.SubjectId), other = knowledge.Person(fact.OtherId);
            if (fact.Kind == "requested_food" && subject != null && (fact.EventTurn > subject.FoodNeedTurn ||
                (fact.EventTurn == subject.FoodNeedTurn && fact.Confidence > subject.FoodConfidence)))
            { subject.FoodNeed = 100; subject.FoodNeedTurn = fact.EventTurn; subject.FoodConfidence = fact.Confidence;
                subject.NeedCause = fact.EventId; subject.NeedStory = fact.StoryId; }
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ThreatTurn ||
                (fact.EventTurn == other.ThreatTurn && fact.Confidence > other.ThreatConfidence)))
            { other.Danger = 100; other.ThreatTurn = fact.EventTurn; other.ThreatConfidence = fact.Confidence; other.ThreatCause = fact.EventId; }
            if ((fact.Kind == "attack" || fact.Kind == "murder") && other != null && (fact.EventTurn > other.ViolationTurn ||
                (fact.EventTurn == other.ViolationTurn && fact.Confidence > other.ViolationConfidence)) && fact.EventId != other.AcknowledgedViolation)
            { other.Violation = 100; other.ViolationTurn = fact.EventTurn; other.ViolationConfidence = fact.Confidence; other.ViolationCause = fact.EventId; }
        }
    }
}
