using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcKnowledgeSystem
    {
        public static bool Visible(RogueGame game, Actor actor, Location place)
        { return place.Map == actor.Location.Map && game.Rules.GridDistance(actor.Location.Position, place.Position) <=
            game.Rules.ActorFOV(actor, actor.Location.Map.LocalTime, game.Session.World.Weather) && LOS.CanTraceViewLine(actor.Location, place.Position); }
        public static void Observe(RogueGame game, Actor owner, SignificantEvent source, bool direct)
        {
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            if (source.Kind == "food_offered" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Request.Id && goal.StoryId == source.StoryId && goal.Plan != null)
                    { goal.Plan.Desired = (ulong)NpcPlanFact.Food; goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = source.Turn; }
            if (direct && source.StoryId != null)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.Plan != null && goal.StoryId == source.StoryId && source.Id > goal.Plan.LastEventId)
                        goal.Plan.LastEventId = source.Id;
            if (source.Kind == "group_succession" && source.Subject != null && source.Subject.SocialGroup != null)
            { RelationshipRecord knownGroup = owner.Personality.Group(source.Subject.SocialGroup.Identity);
                if (knownGroup != null) knownGroup.Name = source.Subject.SocialGroup.LeaderName; }
            bool seesSubject = source.Subject != null && (owner == source.Subject || Visible(game, owner, source.Subject.Location));
            bool seesOther = source.Other != null && (owner == source.Other || Visible(game, owner, source.Other.Location));
            if (seesSubject) knowledge.See(source.Subject, source.Turn);
            if (seesOther) knowledge.See(source.Other, source.Turn);
            if (source.Kind == "shared_food" && seesSubject && seesOther && source.Subject != owner && source.StoryId != null)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.TargetId == source.Other.PersonalityIdentity && goal.StoryId == source.StoryId)
                    {
                        if (goal.DefinitionId == NpcIntentContent.Help.Id)
                            NpcIntentSystem.Finish(owner, goal, NpcIntentStatus.Completed, "observed that the recipient received food");
                        else if (goal.DefinitionId == NpcIntentContent.Gather.Id)
                        {
                            goal.Progress = 2; goal.NextAttempt = source.Turn;
                            if (goal.Plan != null) { goal.Plan.LastEventId = source.Id; goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = source.Turn; }
                        }
                    }
            if (source.Kind == "death" && source.Subject != null)
            { NpcKnownPerson dead = knowledge.Person(source.Subject.PersonalityIdentity); if (dead != null) dead.Dead = true; }
            if (source.Kind == "attack" || source.Kind == "murder" || source.Kind == "death" || source.Kind == "requested_food" || source.Kind == "food_offered" ||
                source.Kind == "army_supplies" || source.Kind.EndsWith("_raid", StringComparison.Ordinal))
                knowledge.Learn(new NpcFact { EventId = source.Id, Kind = source.Kind, EventTurn = source.Turn, LearnedTurn = source.Turn,
                    Source = direct ? NpcKnowledgeSource.Participant : NpcKnowledgeSource.Witness, Confidence = direct ? 100 : 90,
                    SourceId = owner.PersonalityIdentity, SubjectId = !seesSubject ? Guid.Empty : source.Subject.PersonalityIdentity,
                    OtherId = !seesOther ? Guid.Empty : source.Other.PersonalityIdentity,
                    SubjectName = !seesSubject ? null : source.Subject.UnmodifiedName,
                    OtherName = !seesOther ? null : source.Other.UnmodifiedName, Place = new Location(source.Map, source.Position), StoryId = source.StoryId });
            if (seesOther) Social(owner, source);
            if (source.Kind == "supplies_requested" && source.Other == owner && source.Task != null)
                NpcStorySystem.AcceptTask(game, owner, source);
            if (source.Kind == "supplies_requested" && source.Task != null && source.Task.BeneficiaryId == owner.PersonalityIdentity)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Request.Id) goal.Deadline = Math.Max(goal.Deadline, source.Task.Deadline);
            if (source.Kind == "supplies_delivered" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Coordinate.Id && goal.StoryId == source.StoryId)
                        NpcIntentSystem.Finish(owner, goal, NpcIntentStatus.Completed, "collector reported successful delivery");
            if (source.Kind == "task_declined" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.DefinitionId == NpcIntentContent.Coordinate.Id && goal.StoryId == source.StoryId)
                        NpcIntentSystem.Finish(owner, goal, NpcIntentStatus.Failed, "collector declined the task");
            if (source.Kind == "shelter_suggested" && owner.SocialGroup != null && source.Subject != null &&
                owner.SocialGroup == source.Subject.SocialGroup && source.Task != null)
                NpcStorySystem.AcceptShelter(game, owner, source);
        }
        static void Social(Actor owner, SignificantEvent source)
        {
            if (source.Kind == "helped" && source.Subject == owner && source.Other != null)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(trust: 5, attachment: 4, debt: 10);
            if (source.Kind == "shared_food" && source.Subject == owner && source.Other != null)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(attachment: 2, debt: -10);
            if ((source.Kind == "attack" || source.Kind == "murder") && source.Other != null && source.Other != owner)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(
                    fear: source.Subject == owner ? 15 : 5, grievance: source.Subject == owner ? 20 : 3);
        }
        public static void Perceive(RogueGame game, Actor actor, IList<Percept> percepts)
        {
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            int turn = actor.Location.Map.LocalTime.TurnCounter; knowledge.Expire(turn);
            if (percepts != null) foreach (Percept percept in percepts)
            {
                if (percept.Turn != turn || !Visible(game, actor, percept.Location)) continue;
                Actor person = percept.Percepted as Actor; if (person != null) knowledge.See(person, turn);
                Inventory items = percept.Percepted as Inventory;
                if (items != null)
                {
                    int units = 0;
                    XpdBase claim = percept.Location.Map.XpdBaseAt(percept.Location.Position);
                    int risk = claim != null && !claim.Owns(actor) ? 1 : 0;
                    foreach (Item item in items.Items) if (item is ItemFood && !game.Rules.IsFoodSpoiled((ItemFood)item, turn)) units += item.Quantity;
                    NpcKnownPlace old = knowledge.Places.Find(p => p.Kind == "food" && p.Place == percept.Location);
                    if (units > 0 && (old == null || old.Units != units || turn - old.SeenTurn > 180))
                        knowledge.Learn(new NpcFact { Kind = "food_cache", EventId = Session.Get.NextPersonalityEventId(), EventTurn = turn, LearnedTurn = turn,
                            Confidence = 90, Source = NpcKnowledgeSource.Witness, SourceId = actor.PersonalityIdentity, Place = percept.Location, Units = units, Risk = risk });
                    knowledge.RememberPlace(new NpcKnownPlace(percept.Location, "food", turn, units, risk));
                }
            }
            Map map = actor.Location.Map;
            foreach (var entry in map.ExitEntries)
                if (Visible(game, actor, new Location(map, entry.Key)))
                {
                    Location from = new Location(map, entry.Key), to = new Location(entry.Value.ToMap, entry.Value.ToPosition);
                    knowledge.Exits.RemoveAll(e => e.From == from); knowledge.Exits.Add(new NpcKnownExit(from, to));
                    if (knowledge.Exits.Count > 32) knowledge.Exits.RemoveAt(0);
                }
            if (map.GetTileAt(actor.Location.Position).IsInside)
                knowledge.RememberPlace(new NpcKnownPlace(actor.Location, "shelter", turn));
        }
        public static bool Hear(RogueGame game, Actor listener, Actor speaker, NpcFact source)
        {
            RelationshipRecord trust = listener.Personality.Person(speaker.PersonalityIdentity);
            int confidence = Math.Max(0, Math.Min(95, source.Confidence - 20 + (trust == null ? 0 : trust.Trust / 10) +
                Math.Min(0, PersonalitySystem.Bias(listener, DecisionKind.Group)) / 2));
            NpcFact fact = source.Retell(speaker.PersonalityIdentity, listener.Location.Map.LocalTime.TurnCounter, confidence);
            bool learned = listener.Personality.Knowledge.Learn(fact);
            if (learned && fact.Kind == "food_cache" && confidence >= 40)
                listener.Personality.Knowledge.RememberPlace(new NpcKnownPlace(fact.Place, "food", fact.EventTurn, fact.Units, fact.Risk));
            if (learned && fact.SubjectId != Guid.Empty)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.SubjectId, Name = fact.SubjectName, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told, Dead = fact.Kind == "death" && confidence >= 60 });
            if (learned && fact.OtherId != Guid.Empty && listener.Personality.Knowledge.Person(fact.OtherId) == null)
                listener.Personality.Knowledge.LearnPerson(new NpcKnownPerson { Id = fact.OtherId, Name = fact.OtherName, Place = fact.Place,
                    SeenTurn = fact.EventTurn, Confidence = confidence, Source = NpcKnowledgeSource.Told });
            if (learned && fact.Kind == "death")
            {
                NpcKnownPerson person = listener.Personality.Knowledge.Person(fact.SubjectId);
                bool accepted = person != null && person.Dead && person.Source == NpcKnowledgeSource.Told && person.SeenTurn == fact.EventTurn;
                if (accepted) foreach (NpcIntent intent in listener.Personality.Intents)
                    if (!intent.Finished && intent.TargetId == fact.SubjectId) NpcIntentSystem.Finish(listener, intent, NpcIntentStatus.Failed, "learned of death through a report");
            }
            return learned;
        }
        public static void HearLocation(Actor listener, Actor speaker, NpcKnownPerson report, long eventId)
        {
            if (listener.Personality == null) listener.Personality = new PersonalityState();
            var learned = new NpcKnownPerson { Id = report.Id, Name = report.Name, Place = report.Place, SeenTurn = report.SeenTurn,
                Dead = report.Dead && report.Confidence >= 80, Confidence = Math.Max(0, report.Confidence - 20), Source = NpcKnowledgeSource.Told };
            bool accepted = listener.Personality.Knowledge.LearnPerson(learned);
            listener.Personality.Knowledge.Learn(new NpcFact { Kind = "person_location", EventId = eventId, SubjectId = report.Id,
                SubjectName = report.Name, Place = report.Place, Confidence = learned.Confidence, Source = NpcKnowledgeSource.Told,
                SourceId = speaker.PersonalityIdentity, EventTurn = report.SeenTurn, LearnedTurn = listener.Location.Map.LocalTime.TurnCounter });
            if (accepted && learned.Dead) foreach (NpcIntent intent in listener.Personality.Intents)
                if (!intent.Finished && intent.TargetId == learned.Id) NpcIntentSystem.Finish(listener, intent, NpcIntentStatus.Failed, "companion was reported dead");
        }
    }
}
