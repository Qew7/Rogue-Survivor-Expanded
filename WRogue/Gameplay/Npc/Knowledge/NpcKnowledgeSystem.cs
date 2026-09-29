using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcKnowledgeSystem
    {
        public static bool Visible(RogueGame game, Actor actor, Location place)
        { return place.Map == actor.Location.Map && game.Rules.GridDistance(actor.Location.Position, place.Position) <=
            game.Rules.ActorFOV(actor, actor.Location.Map.LocalTime, game.Session.World.Weather) && LOS.CanTraceViewLine(actor.Location, place.Position); }
        internal static void ObserveParticipants(NpcObservation observation)
        {
            Actor owner = observation.Owner; SignificantEvent source = observation.Source;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            if (observation.SeesSubject) knowledge.See(source.Subject, source.Turn);
            if (observation.SeesOther) knowledge.See(source.Other, source.Turn);
            if (observation.SeesSubject) knowledge.Person(source.Subject.PersonalityIdentity).Hostile = observation.Game.Rules.AreEnemies(owner, source.Subject);
            if (observation.SeesOther) knowledge.Person(source.Other.PersonalityIdentity).Hostile = observation.Game.Rules.AreEnemies(owner, source.Other);
            if (observation.Game.NpcContent.Event(source.Kind) != null && observation.Game.NpcContent.Event(source.Kind).ProvesDeath && source.Subject != null)
            { NpcKnownPerson dead = knowledge.Person(source.Subject.PersonalityIdentity); if (dead != null) dead.Dead = true; }
        }
        internal static void RetainEvent(NpcObservation observation, NpcEventDefinition definition)
        {
            if (!definition.RetainFact) return;
            Actor owner = observation.Owner; SignificantEvent source = observation.Source;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            owner.Personality.Knowledge.Learn(new NpcFact { EventId = source.Id, Kind = source.Kind, EventTurn = source.Turn, LearnedTurn = source.Turn,
                Source = observation.Direct ? NpcKnowledgeSource.Participant : NpcKnowledgeSource.Witness, Confidence = observation.Direct ? 100 : 90,
                SourceId = owner.PersonalityIdentity, SubjectId = !seesSubject ? Guid.Empty : source.Subject.PersonalityIdentity,
                OtherId = !seesOther ? Guid.Empty : source.Other.PersonalityIdentity, SubjectName = !seesSubject ? null : source.Subject.UnmodifiedName,
                OtherName = !seesOther ? null : source.Other.UnmodifiedName, Place = new Location(source.Map, source.Position), StoryId = source.StoryId });
        }
        public static void Perceive(RogueGame game, Actor actor, IList<Percept> percepts)
        {
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            int turn = actor.Location.Map.LocalTime.TurnCounter; knowledge.Expire(turn);
            if (percepts != null) foreach (Percept percept in percepts)
            {
                if (percept.Turn != turn || !Visible(game, actor, percept.Location)) continue;
                Actor person = percept.Percepted as Actor;
                if (person != null)
                {
                    knowledge.See(person, turn); NpcKnownPerson known = knowledge.Person(person.PersonalityIdentity);
                    known.Hostile = game.Rules.AreEnemies(actor, person);
                    game.NpcContent.Perceive(NpcPerceptionKind.Person, new NpcPerceptionContext(game, actor, percept.Location, person));
                }
                Inventory items = percept.Percepted as Inventory;
                if (items != null)
                {
                    XpdBase claim = percept.Location.Map.XpdBaseAt(percept.Location.Position);
                    int risk = claim != null && !claim.Owns(actor) ? 1 : 0;
                    game.NpcContent.Perceive(NpcPerceptionKind.Items, new NpcPerceptionContext(game, actor, percept.Location, items: items, risk: risk));
                    foreach (TraitInstance trait in actor.Personality.Traits)
                    {
                        TraitDefinition definition = game.NpcContent.Personalities.Trait(trait.Id);
                        if (definition != null && definition.Interest != null) definition.Interest.Observe(actor, trait, items, percept.Location, risk);
                    }
                }
            }
            game.NpcContent.Perceive(NpcPerceptionKind.Surroundings, new NpcPerceptionContext(game, actor, actor.Location));
        }
        public static bool Hear(RogueGame game, Actor listener, Actor speaker, NpcFact source)
        {
            RelationshipRecord trust = listener.Personality.Person(speaker.PersonalityIdentity);
            int confidence = Math.Max(0, Math.Min(95, source.Confidence - 20 + (trust == null ? 0 : trust.Trust / 10) +
                Math.Min(0, PersonalitySystem.Bias(listener, DecisionKind.Group)) / 2));
            NpcFact fact = source.Retell(speaker.PersonalityIdentity, listener.Location.Map.LocalTime.TurnCounter, confidence);
            NpcFact previous = listener.Personality.Knowledge.Facts.Find(f => f.EventId == fact.EventId && f.Kind == fact.Kind);
            int improvement = Math.Max(0, confidence - (previous == null ? 0 : previous.Confidence));
            bool learned = listener.Personality.Knowledge.Learn(fact);
            if (learned && fact.SubjectId != Guid.Empty && !fact.NoSubjectLocation)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.SubjectId, Name = fact.SubjectName, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told, Dead = game.NpcContent.Event(fact.Kind) != null && game.NpcContent.Event(fact.Kind).ProvesDeath && confidence >= 60 });
            if (learned && fact.OtherId != Guid.Empty && listener.Personality.Knowledge.Person(fact.OtherId) == null)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.OtherId, Name = fact.OtherName, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told });
            if (learned) game.NpcContent.Hear(new NpcReportContext(listener, fact, improvement, game.NpcContent));
            return learned;
        }
        public static void HearLocation(Actor listener, Actor speaker, NpcKnownPerson report, long eventId, NpcContentCatalog catalog)
        {
            if (listener.Personality == null) listener.Personality = new PersonalityState();
            var learned = new NpcKnownPerson { Id = report.Id, Name = report.Name, Place = report.Place, SeenTurn = report.SeenTurn,
                Dead = report.Dead && report.Confidence >= 80, Confidence = Math.Max(0, report.Confidence - 20), Source = NpcKnowledgeSource.Told };
            bool accepted = listener.Personality.Knowledge.LearnPerson(learned);
            listener.Personality.Knowledge.Learn(new NpcFact { Kind = "person_location", EventId = eventId, SubjectId = report.Id,
                SubjectName = report.Name, Place = report.Place, Confidence = learned.Confidence, Source = NpcKnowledgeSource.Told,
                SourceId = speaker.PersonalityIdentity, EventTurn = report.SeenTurn, LearnedTurn = listener.Location.Map.LocalTime.TurnCounter });
            if (accepted && learned.Dead) NpcGoalLifecycle.KnownDeath(listener, learned.Id, "companion was reported dead", catalog);
        }
    }
}
