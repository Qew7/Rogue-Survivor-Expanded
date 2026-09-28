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
        public Guid SubjectId, OtherId, SourceId;
        public int EventTurn, LearnedTurn, Confidence, Hops, Units;
        public Location Place;
        public NpcKnowledgeSource Source;
        public NpcFact Retell(Guid speaker, int turn, int confidence)
        {
            return new NpcFact { EventId = EventId, Kind = Kind, SubjectName = SubjectName, OtherName = OtherName,
                StoryId = StoryId, SubjectId = SubjectId, OtherId = OtherId, SourceId = speaker, EventTurn = EventTurn,
                LearnedTurn = turn, Confidence = confidence, Hops = Hops + 1, Units = Units, Place = Place, Source = NpcKnowledgeSource.Told };
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
    }
    [Serializable]
    sealed class NpcKnownPlace
    {
        public Location Place;
        public string Kind;
        public int SeenTurn, Units;
        public NpcKnownPlace(Location place, string kind, int turn, int units = 0)
        { Place = place; Kind = kind; SeenTurn = turn; Units = units; }
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
        public int NextTalkTurn, NextPlanTurn;
        public bool Learn(NpcFact fact)
        {
            NpcFact old = Facts.Find(f => f.EventId == fact.EventId && f.Kind == fact.Kind);
            if (old != null)
            {
                if (old.Confidence >= fact.Confidence) return false;
                Facts.Remove(old);
            }
            Facts.Add(fact); Trim(Facts, 48); return true;
        }
        public NpcKnownPerson Person(Guid id) { return People.Find(p => p.Id == id); }
        public void See(Actor actor, int turn)
        {
            NpcKnownPerson person = Person(actor.PersonalityIdentity);
            if (person == null) { People.Add(person = new NpcKnownPerson { Id = actor.PersonalityIdentity }); Trim(People, 32); }
            person.Name = actor.UnmodifiedName; person.Place = actor.Location; person.SeenTurn = turn; person.Dead = actor.IsDead;
            person.Confidence = 100; person.Source = NpcKnowledgeSource.Witness;
        }
        public void RememberPlace(NpcKnownPlace place)
        {
            Places.RemoveAll(p => p.Kind == place.Kind && p.Place == place.Place);
            Places.Add(place); Trim(Places, 16);
        }
        public bool LearnPerson(NpcKnownPerson person)
        {
            NpcKnownPerson old = Person(person.Id);
            if (old != null && (old.SeenTurn > person.SeenTurn || (old.SeenTurn == person.SeenTurn && old.Confidence >= person.Confidence))) return false;
            if (old != null) People.Remove(old);
            People.Add(person); Trim(People, 32); return true;
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
