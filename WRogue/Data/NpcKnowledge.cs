using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    enum NpcKnowledgeSource { Participant, Witness, Told, Inferred }
    [Serializable]
    sealed class NpcFact
    {
        public long EventId;
        public string Kind, SubjectName, OtherName, StoryId;
        [System.Runtime.Serialization.OptionalField] public string Resource;
        // The words used by the first observer stay fixed when the fact is retold.
        [System.Runtime.Serialization.OptionalField] public string SubjectReportName;
        [System.Runtime.Serialization.OptionalField] public string OtherReportName;
        [System.Runtime.Serialization.OptionalField] public int? SubjectFactionId;
        [System.Runtime.Serialization.OptionalField] public int? OtherFactionId;
        public string ReportSubject { get { return SubjectReportName ?? SubjectName; } }
        public string ReportOther { get { return OtherReportName ?? OtherName; } }
        public bool NamesSubject { get { return SubjectId != Guid.Empty && !String.IsNullOrEmpty(SubjectName) && ReportSubject == SubjectName; } }
        public bool NamesOther { get { return OtherId != Guid.Empty && !String.IsNullOrEmpty(OtherName) && ReportOther == OtherName; } }
        public Guid SubjectId, OtherId, SourceId;
        public int EventTurn, LearnedTurn, Confidence, Hops, Units, Risk;
        public Location Place;
        public NpcKnowledgeSource Source;
        public bool NoSubjectLocation;
        public NpcFact Retell(Guid speaker, int turn, int confidence)
        {
            return new NpcFact { EventId = EventId, Kind = Kind, SubjectName = SubjectName, OtherName = OtherName,
                SubjectReportName = SubjectReportName, OtherReportName = OtherReportName,
                SubjectFactionId = SubjectFactionId, OtherFactionId = OtherFactionId,
                StoryId = StoryId, Resource = Resource, SubjectId = SubjectId, OtherId = OtherId, SourceId = speaker, EventTurn = EventTurn,
                LearnedTurn = turn, Confidence = confidence, Hops = Hops + 1, Units = Units, Risk = Risk, Place = Place, Source = NpcKnowledgeSource.Told, NoSubjectLocation = NoSubjectLocation };
        }
    }
    [Serializable]
    sealed class NpcKnownPerson
    {
        public Guid Id;
        public string Name;
        public Location Place;
        public int SeenTurn;
        public int Confidence;
        public NpcKnowledgeSource Source;
        public bool Dead;
        public bool Hostile;
        public int FoodNeed, FoodNeedTurn, FoodConfidence, Danger, Violation, ThreatTurn, ThreatConfidence, ReciprocityTurn;
        public long NeedCause, ThreatCause, SocialCause;
        public string NeedStory;
        public int ViolationTurn, ViolationConfidence;
        public long ViolationCause;
        public long AcknowledgedViolation;
        public int MedicalNeed, MedicalTurn, MedicalConfidence, Wounds, FactionId = -1;
        public Guid GroupId;
        public long MedicalCause;
        public string MedicalStory;
        public int LossUnits;
        public long LossCause;
        public Location LossPlace;
    }
    [Serializable]
    sealed class NpcKnownPlace
    {
        public Location Place;
        public string Kind;
        public int SeenTurn, Units, Risk;
        public NpcKnownPlace(Location place, string kind, int turn, int units = 0, int risk = 0)
        { Place = place; Kind = kind; SeenTurn = turn; Units = units; Risk = risk; }
    }
    [Serializable]
    sealed class NpcKnownExit
    {
        public Location From, To;
        public NpcKnownExit(Location from, Location to) { From = from; To = to; }
    }
    [Serializable]
    sealed class NpcKnowledge
    {
        public readonly List<NpcFact> Facts = new List<NpcFact>();
        public readonly List<NpcKnownPerson> People = new List<NpcKnownPerson>();
        public readonly List<NpcKnownPlace> Places = new List<NpcKnownPlace>();
        public readonly List<NpcKnownExit> Exits = new List<NpcKnownExit>();
        readonly Dictionary<string, int> told = new Dictionary<string, int>();
        public int NextTalkTurn, NextPlanTurn, Revision;
        public bool Learn(NpcFact fact)
        {
            for (int i = 0; i < Facts.Count; i++)
            {
                NpcFact old = Facts[i];
                if (old.EventId != fact.EventId || old.Kind != fact.Kind) continue;
                if (old.Confidence >= fact.Confidence) return false;
                Facts.RemoveAt(i);
                break;
            }
            Facts.Add(fact); Trim(Facts, 48); Revision++; return true;
        }
        public NpcKnownPerson Person(Guid id)
        {
            for (int i = 0; i < People.Count; i++)
            {
                NpcKnownPerson person = People[i];
                if (person.Id == id) return person;
            }
            return null;
        }
        public NpcKnownPerson See(Actor actor, int turn)
        {
            NpcKnownPerson person = Person(actor.PersonalityIdentity);
            if (person == null || person.Place != actor.Location || person.Dead != actor.IsDead) Revision++;
            // Refresh recency before another participant can fill the bounded list.
            if (person == null) person = new NpcKnownPerson { Id = actor.PersonalityIdentity };
            else People.Remove(person);
            People.Add(person); Trim(People, 32);
            person.Name = actor.UnmodifiedName; person.Place = actor.Location; person.SeenTurn = turn; person.Dead = actor.IsDead;
            person.FactionId = actor.Faction == null ? -1 : actor.Faction.ID;
            person.GroupId = actor.SocialGroup == null ? Guid.Empty : actor.SocialGroup.Identity;
            person.Confidence = 100; person.Source = NpcKnowledgeSource.Witness;
            return person;
        }
        public void RememberPlace(NpcKnownPlace place)
        {
            NpcKnownPlace old = Places.Find(p => p.Kind == place.Kind && p.Place == place.Place);
            if (old == null || old.Units != place.Units || old.Risk != place.Risk) Revision++;
            Places.RemoveAll(p => p.Kind == place.Kind && p.Place == place.Place);
            Places.Add(place); Trim(Places, 16);
        }
        public bool LearnPerson(NpcKnownPerson person)
        {
            NpcKnownPerson old = Person(person.Id);
            if (old != null && (old.SeenTurn > person.SeenTurn || (old.SeenTurn == person.SeenTurn && old.Confidence >= person.Confidence))) return false;
            if (old != null)
            {
                person.Hostile = old.Hostile; person.FoodNeed = old.FoodNeed; person.FoodNeedTurn = old.FoodNeedTurn;
                person.FoodConfidence = old.FoodConfidence; person.NeedCause = old.NeedCause; person.NeedStory = old.NeedStory;
                person.Danger = old.Danger; person.Violation = old.Violation; person.ThreatTurn = old.ThreatTurn;
                person.ThreatConfidence = old.ThreatConfidence; person.ThreatCause = old.ThreatCause;
                person.ViolationTurn = old.ViolationTurn; person.ViolationConfidence = old.ViolationConfidence; person.ViolationCause = old.ViolationCause;
                person.AcknowledgedViolation = old.AcknowledgedViolation;
                person.SocialCause = old.SocialCause; person.ReciprocityTurn = old.ReciprocityTurn;
                person.MedicalNeed = old.MedicalNeed; person.MedicalTurn = old.MedicalTurn; person.MedicalConfidence = old.MedicalConfidence;
                person.Wounds = old.Wounds; person.MedicalCause = old.MedicalCause; person.MedicalStory = old.MedicalStory;
                person.FactionId = old.FactionId; person.GroupId = old.GroupId;
                person.LossUnits = old.LossUnits; person.LossCause = old.LossCause; person.LossPlace = old.LossPlace;
                People.Remove(old);
            }
            People.Add(person); Trim(People, 32); Revision++; return true;
        }
        public bool WasTold(long eventId, Guid recipient) { return told.ContainsKey(eventId + ":" + recipient); }
        public void Told(long eventId, Guid recipient, int turn)
        {
            if (told.Count >= 64)
            {
                string oldest = null; int time = Int32.MaxValue;
                foreach (var pair in told) if (pair.Value < time) { oldest = pair.Key; time = pair.Value; }
                if (oldest != null) told.Remove(oldest);
            }
            told[eventId + ":" + recipient] = turn;
        }
        public void Expire(int turn)
        {
            Facts.RemoveAll(f => turn - f.EventTurn > 2 * WorldTime.TURNS_PER_DAY);
            Places.RemoveAll(p => turn - p.SeenTurn > 2 * WorldTime.TURNS_PER_DAY);
        }
        static void Trim<T>(List<T> list, int max) { if (list.Count > max) list.RemoveAt(0); }
    }
    sealed partial class PersonalityState
    {
        NpcKnowledge m_Knowledge;
        public NpcKnowledge Knowledge { get { return m_Knowledge ?? (m_Knowledge = new NpcKnowledge()); } }
        internal bool HasKnowledge { get { return m_Knowledge != null; } }
        public RelationshipRecord Opinion(Guid id, string name)
        {
            RelationshipRecord person = Person(id);
            if (person == null) PersonRecords.Add(id, person = new RelationshipRecord(id, -1, name));
            return person;
        }
    }
}
