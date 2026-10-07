using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcKnowledgeSystem
    {
        static string ReportName(Actor observer, Actor participant, bool seen)
        {
            if (participant == null) return null;
            if (!seen) return "someone";
            RelationshipRecord relationship = observer.Personality.Person(participant.PersonalityIdentity);
            if (participant == observer || relationship != null && relationship.Name == participant.UnmodifiedName)
                return participant.UnmodifiedName;
            return participant.Faction == null ? "someone" : "a " + participant.Faction.MemberName;
        }
        public static bool Visible(RogueGame game, Actor actor, Location place)
        { return place.Map == actor.Location.Map && Visible(game, actor, place,
            game.Rules.ActorFOV(actor, actor.Location.Map.LocalTime, game.Session.World.Weather)); }
        internal static bool Visible(RogueGame game, Actor actor, Location place, int maxRange)
        { return place.Map == actor.Location.Map && game.Rules.GridDistance(actor.Location.Position, place.Position) <= maxRange &&
            LOS.CanTraceViewLine(actor.Location, place.Position); }
        internal static void ObserveParticipants(NpcObservation observation)
        {
            Actor owner = observation.Owner; SignificantEvent source = observation.Source;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson subject = observation.SeesSubject ? knowledge.See(source.Subject, source.Turn) : null;
            NpcKnownPerson other = observation.SeesOther ? knowledge.See(source.Other, source.Turn) : null;
            if (subject != null) subject.Hostile = observation.Game.Rules.AreEnemies(owner, source.Subject);
            if (other != null) other.Hostile = observation.Game.Rules.AreEnemies(owner, source.Other);
            NpcEventDefinition definition = observation.Game.NpcContent.Event(source.Kind);
            if (definition != null && definition.ProvesDeath && source.Subject != null)
            {
                NpcKnownPerson dead = subject ?? knowledge.Person(source.Subject.PersonalityIdentity);
                if (dead != null) dead.Dead = true;
            }
        }
        internal static void RetainEvent(NpcObservation observation, NpcEventDefinition definition)
        {
            if (!definition.RetainFact) return;
            Actor owner = observation.Owner; SignificantEvent source = observation.Source;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            bool knowsClaimant = source.ClaimantId != Guid.Empty &&
                (owner.PersonalityIdentity == source.ClaimantId ||
                 owner.SocialGroup != null && owner.SocialGroup.Identity == source.ClaimantGroupId ||
                 owner.Personality.Person(source.ClaimantId) != null || owner.Personality.Knowledge.Person(source.ClaimantId) != null);
            owner.Personality.Knowledge.Learn(new NpcFact { EventId = source.Id, Kind = source.Kind, EventTurn = source.Turn, LearnedTurn = source.Turn,
                Source = observation.Direct ? NpcKnowledgeSource.Participant : NpcKnowledgeSource.Witness, Confidence = observation.Direct ? 100 : 90,
                SourceId = owner.PersonalityIdentity, SubjectId = !seesSubject ? Guid.Empty : source.Subject.PersonalityIdentity,
                OtherId = knowsClaimant ? source.ClaimantId : !seesOther ? Guid.Empty : source.Other.PersonalityIdentity, SubjectName = !seesSubject ? null : source.Subject.UnmodifiedName,
                OtherName = knowsClaimant ? source.ClaimantName : !seesOther ? null : source.Other.UnmodifiedName,
                SubjectReportName = ReportName(owner, source.Subject, seesSubject),
                OtherReportName = knowsClaimant ? source.ClaimantName : ReportName(owner, source.Other, seesOther),
                SubjectFactionId = !seesSubject || source.Subject.Faction == null ? (int?)null : source.Subject.Faction.ID,
                OtherFactionId = !seesOther || source.Other.Faction == null ? (int?)null : source.Other.Faction.ID,
                Place = new Location(source.Map, source.Position), StoryId = source.StoryId,
                Resource = source.Resource, ClaimantGroupId = source.ClaimantGroupId,
                ItemId = source.ItemId, Units = source.Units });
        }
        public static void Perceive(RogueGame game, Actor actor, IList<Percept> percepts,
            HashSet<Point> currentFov = null)
        {
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            int turn = actor.Location.Map.LocalTime.TurnCounter; knowledge.Expire(turn);
            if (percepts != null) foreach (Percept percept in percepts)
            {
                if (percept.Turn != turn || percept.Location.Map != actor.Location.Map ||
                    (currentFov == null ? !Visible(game, actor, percept.Location) :
                        !currentFov.Contains(percept.Location.Position))) continue;
                Actor person = percept.Percepted as Actor;
                if (person != null)
                {
                    if (person.IsDead || person.Location != percept.Location) continue;
                    NpcKnownPerson known = knowledge.See(person, turn);
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
            int confidence = ReportConfidence(listener, speaker, source);
            NpcKnowledge knowledge = listener.Personality.Knowledge;
            NpcFact previous = knowledge.Facts.Find(f => f.EventId == source.EventId && f.Kind == source.Kind);
            if (previous != null && previous.Confidence >= confidence) return false;
            NpcFact fact = source.Retell(speaker.PersonalityIdentity, listener.Location.Map.LocalTime.TurnCounter, confidence);
            bool refuted = false;
            if (fact.Kind == "claimed_permission")
            {
                NpcFact contrary = knowledge.Facts.Find(f => f.EventId == fact.EventId && f.Kind == "base_theft");
                refuted = contrary != null && contrary.Source != NpcKnowledgeSource.Told && contrary.Confidence >= 80;
            }
            int improvement = Math.Max(0, confidence - (previous == null ? 0 : previous.Confidence));
            bool learned = knowledge.Learn(fact);
            if (learned && refuted) NpcTestimony.Refute(game.NpcContent, listener, speaker, fact);
            if (learned && fact.NamesSubject && !fact.NoSubjectLocation)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.SubjectId, Name = fact.ReportSubject, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told, Dead = game.NpcContent.Event(fact.Kind) != null && game.NpcContent.Event(fact.Kind).ProvesDeath && confidence >= 60 });
            if (learned && fact.NamesOther && listener.Personality.Knowledge.Person(fact.OtherId) == null)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.OtherId, Name = fact.ReportOther, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told });
            if (learned) game.NpcContent.Hear(new NpcReportContext(listener, fact, refuted ? 0 : improvement, game.NpcContent));
            return learned;
        }
        public static int ReportConfidence(Actor listener, Actor speaker, NpcFact source)
        {
            RelationshipRecord trust = listener.Personality.Person(speaker.PersonalityIdentity);
            int confidence = Math.Max(0, Math.Min(95, source.Confidence - 20 + (trust == null ? 0 : trust.Trust / 10) +
                (trust == null ? 0 : -trust.Grievance / 5) +
                Math.Min(0, PersonalitySystem.Bias(listener, DecisionKind.Group)) / 2));
            if (source.Kind == "claimed_permission")
            {
                NpcFact contrary = listener.Personality.Knowledge.Facts.Find(f => f.EventId == source.EventId && f.Kind == "base_theft");
                if (contrary != null && contrary.Source != NpcKnowledgeSource.Told && contrary.Confidence >= 80)
                    return 20;
                if (contrary != null && contrary.Confidence >= confidence) return Math.Min(confidence, 30);
            }
            return confidence;
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
