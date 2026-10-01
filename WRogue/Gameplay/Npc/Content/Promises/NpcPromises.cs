using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcPromises
    {
        public static void Expire(Actor owner, NpcContentCatalog catalog)
        {
            if (owner.Personality == null || !owner.Personality.HasCommitments || owner.IsDead || owner.IsSleeping || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            foreach (NpcCommitment promise in owner.Personality.Commitments)
                if (promise.Status == NpcCommitmentStatus.Active && turn >= promise.DueTurn)
                {
                    promise.Status = NpcCommitmentStatus.Broken;
                    if (promise.Beneficiary != owner.PersonalityIdentity) continue;
                    PrivateAssessment(owner, "promise_broken", promise.Promisor, promise.PromisorName, promise.Id, promise.StoryId, promise, catalog);
                    owner.Personality.Opinion(promise.Promisor, promise.PromisorName).AdjustSocial(trust: -20, grievance: 15);
                }
        }
        static void PrivateAssessment(Actor owner, string kind, Guid subject, string name, long cause, string story, NpcCommitment promise, NpcContentCatalog catalog)
        {
            int turn = owner.Location.Map.LocalTime.TurnCounter; long id = Session.Get.NextPersonalityEventId();
            var e = new ObservedEvent(kind, turn, name, owner.UnmodifiedName, true, subjectId: subject, otherId: owner.PersonalityIdentity,
                eventId: id, causeId: cause, storyId: story);
            owner.Personality.Remember(e); Session.Get.ResidentRecords.Observe(owner, e);
            owner.Personality.Knowledge.Learn(new NpcFact { EventId = id, Kind = kind, SubjectId = subject, SubjectName = name,
                OtherId = owner.PersonalityIdentity, OtherName = owner.UnmodifiedName, EventTurn = turn, LearnedTurn = turn,
                Confidence = 80, Source = NpcKnowledgeSource.Inferred, SourceId = owner.PersonalityIdentity, Place = owner.Location,
                StoryId = story, NoSubjectLocation = true });
            NpcKnownPerson known = owner.Personality.Knowledge.Person(subject);
            if (known != null) { known.Violation = 100; known.ViolationTurn = turn; known.ViolationConfidence = 80; known.ViolationCause = id; known.SocialCause = id; }
            MemoryDefinition definition = catalog.Personalities.Memory(kind);
            if (definition == null) return;
            NpcMemoryProcessor.Evidence(owner, catalog.Personalities, kind, turn);
            NpcMemoryProcessor.Add(owner, definition, turn, name, null, subjectId: subject,
                relations: new NpcMemoryRelations { Person = subject, PersonName = name, Group = promise.GroupId,
                    GroupName = promise.GroupName, Faction = promise.FactionId, FactionName = promise.FactionName }, impact: definition.FeelingChange);
        }
        public static void Delivery(RogueGame game, Actor giver, Actor recipient, string resource, long cause, string story)
        {
            if (!giver.Personality.HasCommitments) return;
            NpcCommitment promise = giver.Personality.Commitments.Find(p => p.Status == NpcCommitmentStatus.Kept && p.Promisor == giver.PersonalityIdentity &&
                p.Beneficiary == recipient.PersonalityIdentity && p.Resource == resource && !p.OutcomeReported);
            if (promise == null) return;
            promise.OutcomeReported = true;
            NpcEvents.Publish(game, "promise_kept", giver, recipient, promise.Id, story);
            Session.Get.ResidentRecords.LinkStory(giver, promise.StoryId, story, cause);
        }
    }
}
