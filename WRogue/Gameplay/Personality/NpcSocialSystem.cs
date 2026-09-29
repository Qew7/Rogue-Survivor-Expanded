using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcSocialSystem
    {
        public static void Observe(RogueGame game, Actor owner, SignificantEvent source, bool direct)
        {
            NpcKnownPerson subject = source.Subject == null ? null : owner.Personality.Knowledge.Person(source.Subject.PersonalityIdentity);
            NpcKnownPerson other = source.Other == null ? null : owner.Personality.Knowledge.Person(source.Other.PersonalityIdentity);
            if (source.Kind == "requested_medicine" && subject != null)
            { subject.MedicalNeed = 100; subject.MedicalConfidence = 100; subject.MedicalTurn = source.Turn; subject.MedicalCause = source.Id; subject.MedicalStory = source.StoryId; }
            if ((source.Kind == "shared_medicine" || source.Kind == "treated_person") && other != null)
            { other.MedicalNeed = source.Kind == "shared_medicine" ? 0 : Math.Max(0, game.Rules.ActorMaxHPs(source.Other) - source.Other.HitPoints) * 100 / game.Rules.ActorMaxHPs(source.Other);
                other.MedicalTurn = source.Turn; other.Wounds = Math.Max(0, game.Rules.ActorMaxHPs(source.Other) - source.Other.HitPoints) * 100 / game.Rules.ActorMaxHPs(source.Other); owner.Personality.Knowledge.Revision++; }
            if ((source.Kind == "shared_medicine" || source.Kind == "treated_person") && source.Other == owner && subject != null)
            { owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: 5, attachment: 4, debt: 10); subject.SocialCause = source.Id;
                owner.Personality.Attach(new NpcAttachment { Kind = "person", Person = subject.Id, Name = subject.Name, Weight = 20, CauseId = source.Id }); }
            if ((source.Kind == "food_promised" || source.Kind == "medicine_promised") && direct && source.Subject != null && source.Other != null)
                owner.Personality.RememberCommitment(new NpcCommitment { Id = source.Id, CauseId = source.CauseId,
                    Promisor = source.Subject.PersonalityIdentity, Beneficiary = source.Other.PersonalityIdentity,
                    PromisorName = source.Subject.UnmodifiedName, BeneficiaryName = source.Other.UnmodifiedName,
                    GroupId = source.Subject.SocialGroup == null ? Guid.Empty : source.Subject.SocialGroup.Identity,
                    GroupName = source.Subject.SocialGroup == null ? null : source.Subject.SocialGroup.LeaderName,
                    FactionId = source.Subject.Faction.ID, FactionName = source.Subject.Faction.Name,
                    Resource = source.Kind == "food_promised" ? "food" : "medicine", DueTurn = source.Turn + 180, StoryId = source.StoryId });
            if (owner.Personality.HasCommitments && (source.Kind == "shared_food" || source.Kind == "shared_medicine" || source.Kind == "restitution_given"))
                foreach (NpcCommitment promise in owner.Personality.Commitments)
                    if (promise.Status == NpcCommitmentStatus.Active && source.Subject != null && source.Other != null &&
                        promise.Promisor == source.Subject.PersonalityIdentity && promise.Beneficiary == source.Other.PersonalityIdentity &&
                        promise.Resource == (source.Kind == "shared_medicine" ? "medicine" : "food"))
                    { promise.Units = Math.Max(0, promise.Units - Math.Max(1, source.Units)); if (promise.Units == 0) promise.Status = NpcCommitmentStatus.Kept; }
            if (source.Kind == "restitution_given" && subject != null && owner == source.Other)
            {
                foreach (NpcAttachment home in owner.Personality.Attachments)
                    if (home.Kind == "place" && home.Place == source.ResourcePlace && home.Resource == "food" && home.Person == source.Subject.PersonalityIdentity) home.MissingUnits = Math.Max(0, home.MissingUnits - 1);
            }
            if (source.Kind == "base_theft" && other != null && source.Subject == owner && source.Resource == "food")
            { other.LossUnits += Math.Max(1, source.Units); other.LossCause = source.Id; other.LossPlace = new Location(source.Map, source.Position); }
            if (source.Kind == "supplies_lost" && source.Subject == owner)
            {
                Guid culprit = other == null ? Guid.Empty : other.Id;
                owner.Personality.Attach(new NpcAttachment { Kind = "place", Person = culprit, Resource = source.Resource, Place = new Location(source.Map, source.Position), Name = source.Map.Name, Weight = 40 });
                NpcAttachment home = owner.Personality.Attachments.Find(a => a.Kind == "place" && a.Person == culprit && a.Resource == source.Resource && a.Place == new Location(source.Map, source.Position));
                home.MissingUnits += Math.Max(1, source.Units); home.CauseId = source.Id;
            }
            if ((source.Kind == "boundary_accepted" || source.Kind == "boundary_defied" || source.Kind == "restitution_refused") && owner == source.Other && subject != null)
            {
                subject.Violation = source.Kind == "boundary_accepted" ? 0 : 100;
                subject.ViolationCause = source.Id; subject.ViolationTurn = source.Turn; subject.ViolationConfidence = 100;
                owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: source.Kind == "boundary_accepted" ? 5 : -10, grievance: source.Kind == "boundary_accepted" ? -5 : 10);
            }
            if (source.Kind == "contested_taken" && owner == source.Other && subject != null)
            { subject.Violation = 100; subject.ViolationCause = source.Id; subject.ViolationTurn = source.Turn; subject.ViolationConfidence = 100; }
            if (source.Kind == "resource_contested" || source.Kind == "resource_yielded" || source.Kind == "resource_refused" || source.Kind == "contested_taken")
                ObserveDispute(game, owner, source, direct);
            if (source.Kind == "promise_kept" && source.Other == owner && subject != null)
            { owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: 15, attachment: 5); subject.SocialCause = source.Id; }
            if (source.Kind == "confronted" && source.Other == owner && NpcIntentSystem.Enabled(owner))
                Reply(owner, source.Subject, source, PersonalitySystem.Bias(owner, DecisionKind.Law) + PersonalitySystem.Bias(owner, DecisionKind.Compassion) >= 0 ?
                    "boundary_accepted" : "boundary_defied", "I understand. I'll respect that.", "You don't decide what I do.");
            if (source.Kind == "restitution_requested" && source.Other == owner && subject != null && NpcIntentSystem.Enabled(owner))
            {
                bool accept = PersonalitySystem.Bias(owner, DecisionKind.Law) + PersonalitySystem.Bias(owner, DecisionKind.Compassion) >= 10;
                if (accept && subject.LossCause > 0) subject.LossUnits = Math.Max(subject.LossUnits, source.Units);
                Reply(owner, source.Subject, source, accept ? "boundary_accepted" : "restitution_refused", "I'll try to replace what was lost.", "I won't replace your supplies.");
            }
            if (source.Kind == "promise_released" && (owner == source.Subject || owner == source.Other))
                foreach (NpcCommitment promise in owner.Personality.Commitments)
                    if (promise.Status == NpcCommitmentStatus.Active && promise.Id == source.CauseId && promise.Beneficiary == source.Subject.PersonalityIdentity && promise.Promisor == source.Other.PersonalityIdentity)
                    {
                        promise.Status = NpcCommitmentStatus.Released;
                        foreach (NpcIntent intent in owner.Personality.Intents) if (!intent.Finished && intent.Generated != null && intent.Generated.ObligationId == promise.Id)
                            NpcIntentSystem.Finish(owner, intent, NpcIntentStatus.Abandoned, "recipient explicitly released the promise");
                    }
            if ((source.Kind == "shared_food" || source.Kind == "shared_medicine") && source.Other == owner && PersonalitySystem.Bias(owner, DecisionKind.Compassion) > 0)
                ReleaseUnneeded(game, owner, source);
            if (subject != null && source.Kind == "helped" && source.Subject == owner && other != null)
                owner.Personality.Attach(new NpcAttachment { Kind = "person", Person = other.Id, Name = other.Name, Weight = 20, CauseId = source.Id });
        }
        static void Reply(Actor owner, Actor target, SignificantEvent source, string kind, string yes, string no)
        {
            if (target == null || owner.Personality.Reactions.Count >= 4) return;
            owner.Personality.Reactions.Add(new NpcReaction(target, kind.EndsWith("accepted") || kind.EndsWith("yielded") ? yes : no,
                source.Id, source.Turn, kind, source.StoryId));
        }
        public static void Perceive(RogueGame game, Actor owner, Actor person, NpcKnownPerson known, int turn)
        {
            int wounds = Math.Max(0, game.Rules.ActorMaxHPs(person) - person.HitPoints) * 100 / game.Rules.ActorMaxHPs(person);
            if (wounds == 0 && known.Wounds > 0 || wounds > known.Wounds || turn - known.MedicalTurn > 60)
            { known.MedicalNeed = wounds; known.MedicalConfidence = 90; known.MedicalTurn = turn; }
            known.Wounds = wounds;
        }
        public static void RememberHome(Actor owner)
        {
            if (!owner.Location.Map.GetTileAt(owner.Location.Position).IsInside) return;
            XpdBase claim = owner.Location.Map.XpdBaseAt(owner.Location.Position);
            if (claim != null && claim.Owns(owner) && !owner.Personality.Attachments.Exists(a => a.Kind == "place" &&
                a.Place.Map == owner.Location.Map && claim.Contains(a.Place.Position)))
                owner.Personality.Attach(new NpcAttachment { Kind = "place", Place = owner.Location, Name = owner.Location.Map.Name, Weight = 40 });
        }
        public static void Expire(Actor owner)
        {
            if (owner.Personality == null || !owner.Personality.HasCommitments || owner.IsDead || owner.IsSleeping || !Session.Get.GamePreset.NpcPersonalitiesEnabled) return;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            foreach (NpcCommitment promise in owner.Personality.Commitments)
                if (promise.Status == NpcCommitmentStatus.Active && turn >= promise.DueTurn)
                {
                    promise.Status = NpcCommitmentStatus.Broken;
                    if (promise.Beneficiary != owner.PersonalityIdentity) continue;
                    PrivateAssessment(owner, "promise_broken", promise.Promisor, promise.PromisorName, promise.Id, promise.StoryId, promise);
                    owner.Personality.Opinion(promise.Promisor, promise.PromisorName).AdjustSocial(trust: -20, grievance: 15);
                }
        }
        static void PrivateAssessment(Actor owner, string kind, Guid subject, string name, long cause, string story, NpcCommitment promise)
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
            MemoryDefinition definition = PersonalitySystem.Registry.Memory(kind);
            if (definition == null) return;
            var memory = new MemoryInstance(kind, turn, turn + definition.MinDays * WorldTime.TURNS_PER_DAY, name, subjectId: subject, relatedPersonId: subject);
            if (owner.Personality.AddMemory(memory))
            {
                owner.Personality.RememberPerson(subject, name, memory, definition.FeelingChange);
                if (promise.GroupId != Guid.Empty) owner.Personality.RememberGroup(promise.GroupId, promise.GroupName, memory, definition.FeelingChange / 3);
                if (promise.FactionId >= 0 && promise.FactionId != (int)GameFactions.IDs.TheCivilians)
                    owner.Personality.RememberFaction(promise.FactionId, promise.FactionName, memory, definition.FeelingChange / 4);
                Session.Get.ResidentRecords.MemoryStarted(owner, memory);
            }
        }
        public static void Delivery(RogueGame game, Actor giver, Actor recipient, string resource, long cause, string story)
        {
            if (!giver.Personality.HasCommitments) return;
            NpcCommitment promise = giver.Personality.Commitments.Find(p => p.Status == NpcCommitmentStatus.Kept && p.Promisor == giver.PersonalityIdentity &&
                p.Beneficiary == recipient.PersonalityIdentity && p.Resource == resource && !p.OutcomeReported);
            if (promise == null) return;
            promise.OutcomeReported = true;
            NpcIntentSystem.Publish(game, "promise_kept", giver, recipient, promise.Id, story);
            Session.Get.ResidentRecords.LinkStory(giver, promise.StoryId, story, cause);
        }
    }
}
