using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class ServiceExchangeModule : INpcContentModule
    {
        public string Id { get { return "service-exchange"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            var escort = new NpcIntentDefinition("escort_service_shelter", "Escort a patient to shelter", NpcIntentMethod.ReachShelter,
                55, 20, 180, 0, 0) { Result = (c, g) => (ulong)NpcPlanFact.Sheltered,
                TravelArrived = a => a.Location.Map.GetTileAt(a.Location.Position).IsInside,
                TravelDestination = NpcServices.EscortDestination, DirectAction = NpcSafetyActions.Shelter };
            var patient = new NpcIntentDefinition("reach_service_shelter", "Reach shelter with an escort", NpcIntentMethod.ReachShelter,
                55, 20, 180, 0, 0) { Result = (c, g) => (ulong)NpcPlanFact.Sheltered,
                TravelArrived = a => a.Location.Map.GetTileAt(a.Location.Position).IsInside,
                TravelDestination = SafetyPlanOperators.ShelterDestination, DirectAction = NpcSafetyActions.Shelter };
            catalog.Capability(escort); catalog.Capability(patient);
            catalog.Clock(NpcClockPhase.BeforeDecision, c => NpcServices.Advance(c.Game, c.Owner));
            catalog.Clock(NpcClockPhase.MapTurn, c => NpcServices.Expire(c.Game.NpcContent, c.Owner));
            catalog.Event(new NpcEventDefinition("shelter_care_offered", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " offered medicine in exchange for help reaching shelter from " + (e.Other ?? "someone") + ".",
                f => f.ReportSubject + " offered " + (f.ReportOther ?? "someone") + " medicine for help reaching shelter")
                { SelfReportTone = NpcSelfReportTone.Helpful,
                    CanReply = (provider, recipient) => provider.Personality != null && recipient.Personality != null &&
                        provider.Personality.CanRememberService && recipient.Personality.CanRememberService,
                    PlayerReply = new NpcPlayerReply("medicine for a shared trip to shelter", "shelter_care_accepted", "shelter_care_refused",
                        "I'll go with you to shelter.", "I won't make that agreement.") });
            catalog.Event(new NpcEventDefinition("shelter_care_accepted", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " agreed to help " + (e.Other ?? "someone") + " reach shelter in exchange for medicine.",
                f => f.ReportSubject + " agreed to a shelter trip with " + (f.ReportOther ?? "someone") + " for medicine")
                { CanReply = (acceptor, provider) => NpcServices.CanAccept(acceptor, provider) });
            catalog.Event(new NpcEventDefinition("shelter_care_refused", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " declined " + (e.Other ?? "someone") + "'s shelter and medicine agreement.",
                f => f.ReportSubject + " declined " + (f.ReportOther ?? "someone") + "'s shelter and medicine agreement"));
            catalog.Event(new NpcEventDefinition("shelter_care_completed", NpcRecordCategory.Help, true,
                e => (e.Subject ?? "Someone") + " provided medicine after reaching shelter with " + (e.Other ?? "someone") + ".",
                f => f.ReportSubject + " provided medicine to " + (f.ReportOther ?? "someone") + " after a shared shelter trip") { SelfReportTone = NpcSelfReportTone.Helpful });
            catalog.Event(new NpcEventDefinition("shelter_care_failed", NpcRecordCategory.Help, false,
                e => (e.Other ?? "Someone") + " concluded that " + (e.Subject ?? "someone") + " did not complete their shelter and medicine agreement.",
                f => f.ReportSubject + " did not complete a shelter and medicine agreement", isPrivate: true));
            catalog.OnReport("shelter_care_completed", c => NpcReputation.Help(c));
            catalog.OnReport("shelter_care_failed", c => NpcReputation.Reputation(c, false, true, false));
            catalog.On("shelter_care_offered", NpcObservationPhase.Relationships, NpcServices.Observe);
            catalog.On("shelter_care_accepted", NpcObservationPhase.Relationships, NpcServices.Observe);
            catalog.On("shelter_care_refused", NpcObservationPhase.Relationships, NpcServices.Observe);
            catalog.On("shelter_care_completed", NpcObservationPhase.Relationships, NpcServices.Observe);
            catalog.On("shelter_care_offered", NpcObservationPhase.Replies, NpcServices.Reply);
            catalog.AfterEvent("shelter_care_accepted", NpcServices.StartTravel);
            catalog.AfterEvent("shared_medicine", NpcServices.Delivery);
            catalog.AfterEvent("treated_person", NpcServices.Delivery);
            NpcMemoryContent.Received(catalog, "shelter_care_received", "Received medicine after a shared shelter trip", "shelter_care_completed", 12, "protector");
            catalog.Memory(new MemoryDefinition("shelter_care_provided", "Kept a shelter and care agreement", 2, 5,
                new[] { new MemoryTrigger("shelter_care_completed", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "reliable", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Other, 4), false);
            catalog.Memory(new MemoryDefinition("shelter_care_failed", "A shelter and care agreement was not completed", 2, 5,
                new[] { new MemoryTrigger("shelter_care_failed", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "disillusioned", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -10), false);
        }
    }

    static class NpcServices
    {
        public static bool TryPrepare(RogueGame game, Actor provider, SignificantEvent request)
        {
            if (request.Kind != "requested_medicine" || request.Other != provider || request.Subject == null ||
                request.Subject.IsDead || request.Subject.Personality == null || provider.Personality.Reactions.Count >= 4 ||
                !provider.Personality.CanRememberService || !request.Subject.Personality.CanRememberService ||
                PersonalitySystem.Bias(provider, DecisionKind.Trade) < 15 ||
                PersonalitySystem.Bias(provider, DecisionKind.Compassion) > 0 ||
                provider.Personality.ServiceAgreements.Exists(a => a.Patient == request.Subject.PersonalityIdentity &&
                    (a.Status == NpcServiceStatus.Offered || a.Status == NpcServiceStatus.Accepted)) ||
                NpcPlanExecution.Medicine(game, provider, provider.Location, true) == null) return false;
            NpcKnownPlace shelter = provider.Personality.Knowledge.Places.Find(p => p.Kind == "shelter" &&
                p.Place.Map == provider.Location.Map && p.Place != provider.Location &&
                provider.Location.Map.GetTileAt(p.Place.Position).IsInside);
            if (shelter == null) return false;
            var reply = new NpcReaction(request.Subject,
                "Come with me to shelter and I'll give you medicine when we arrive.", request.Id, request.Turn,
                "shelter_care_offered", request.StoryId) { ResourcePlace = shelter.Place, Resource = "medicine" };
            provider.Personality.Reactions.Add(reply);
            return true;
        }
        public static void Observe(NpcObservation observation)
        {
            SignificantEvent source = observation.Source;
            Actor owner = observation.Owner;
            if (source.Subject == null || source.Other == null ||
                (owner != source.Subject && owner != source.Other)) return;
            if (source.Kind == "shelter_care_offered")
            {
                if (source.ResourcePlace.Map == null) return;
                owner.Personality.RememberService(new NpcServiceAgreement { Id = source.Id, CauseId = source.CauseId,
                    Provider = source.Subject.PersonalityIdentity, Patient = source.Other.PersonalityIdentity,
                    ProviderName = source.Subject.UnmodifiedName, PatientName = source.Other.UnmodifiedName,
                    Shelter = source.ResourcePlace, DueTurn = source.Turn + 180, StoryId = source.StoryId });
                return;
            }
            if (!owner.Personality.HasServiceAgreements) return;
            NpcServiceAgreement agreement = owner.Personality.ServiceAgreements.Find(a => a.Id == source.CauseId);
            if (agreement == null) return;
            if (source.Kind == "shelter_care_accepted")
            { owner.Personality.SetServiceStatus(agreement, NpcServiceStatus.Accepted); agreement.CauseId = source.Id;
                if (owner.PersonalityIdentity == agreement.Provider)
                    foreach (NpcIntent goal in owner.Personality.Intents)
                        if (!goal.Finished && goal.DefinitionId == "medical_aid" && goal.TargetId == agreement.Patient)
                            NpcIntentSystem.Finish(observation.Game.NpcContent, owner, goal, NpcIntentStatus.Abandoned,
                                "care is promised after the shelter trip");
            }
            else if (source.Kind == "shelter_care_refused") owner.Personality.SetServiceStatus(agreement, NpcServiceStatus.Refused);
            else if (source.Kind == "shelter_care_completed")
            { owner.Personality.SetServiceStatus(agreement, NpcServiceStatus.Completed);
                Actor peer = owner == source.Subject ? source.Other : source.Subject;
                owner.Personality.Opinion(peer.PersonalityIdentity, peer.UnmodifiedName).AdjustSocial(trust: 12, attachment: 5);
            }
        }
        public static void Reply(NpcObservation observation)
        {
            SignificantEvent offer = observation.Source; Actor patient = observation.Owner;
            if (patient != offer.Other || !NpcIntentSystem.Enabled(patient) || offer.Subject == null) return;
            int willingness = PersonalitySystem.Bias(patient, DecisionKind.Trade) + PersonalitySystem.Bias(patient, DecisionKind.Group) +
                PersonalitySystem.Attitude(patient, offer.Subject) / 4 +
                (patient.HitPoints < observation.Game.Rules.ActorMaxHPs(patient) ? 20 : -40);
            NpcReplies.Reply(patient, offer.Subject, offer, willingness >= 0 ? "shelter_care_accepted" : "shelter_care_refused",
                "I'll help you get to shelter.", "I won't make that trip.");
        }
        internal static bool CanAccept(Actor patient, Actor provider, long offerId = 0)
        {
            if (patient == null || provider == null || patient.Personality == null || provider.Personality == null) return false;
            return patient.Personality.ServiceAgreements.Exists(a => a.Status == NpcServiceStatus.Offered &&
                a.Provider == provider.PersonalityIdentity && a.Patient == patient.PersonalityIdentity &&
                (offerId == 0 || a.Id == offerId) &&
                provider.Personality.ServiceAgreements.Exists(b => b.Id == a.Id && b.Status == NpcServiceStatus.Offered));
        }
        public static void StartTravel(RogueGame game, SignificantEvent accepted)
        {
            if (accepted.Subject == null || accepted.Other == null || accepted.Other.Personality == null) return;
            NpcServiceAgreement agreement = accepted.Other.Personality.ServiceAgreements.Find(a => a.Id == accepted.CauseId);
            if (agreement == null || agreement.Status != NpcServiceStatus.Accepted) return;
            Start(game, accepted.Other, "escort_service_shelter", agreement, accepted.Id);
            Start(game, accepted.Subject, "reach_service_shelter", agreement, accepted.Id);
        }
        static void Start(RogueGame game, Actor actor, string capability, NpcServiceAgreement agreement, long cause)
        {
            if (!NpcIntentSystem.Enabled(actor)) return;
            NpcStorySystem.StartKnown(game.NpcContent, actor,
                new NpcKnownPerson { Id = actor.PersonalityIdentity, Name = actor.UnmodifiedName, Place = actor.Location,
                    SeenTurn = actor.Location.Map.LocalTime.TurnCounter }, game.NpcContent.Capability(capability),
                cause, agreement.StoryId, agreement.Shelter);
        }
        public static Location EscortDestination(NpcExecutionContext context)
        {
            Actor owner = context.Owner;
            NpcServiceAgreement agreement = owner.Personality.ServiceAgreements.Find(a => a.CauseId == context.Goal.CauseId &&
                a.Status == NpcServiceStatus.Accepted);
            if (agreement != null)
                foreach (Actor actor in owner.Location.Map.Actors)
                    if (actor.PersonalityIdentity == agreement.Patient && NpcIntentSystem.CanSee(context.Game, owner, actor) &&
                        context.Game.Rules.GridDistance(owner.Location.Position, actor.Location.Position) > 3 &&
                        context.Game.Rules.GridDistance(actor.Location.Position, agreement.Shelter.Position) > 2)
                        return actor.Location;
            return SafetyPlanOperators.ShelterDestination(context);
        }
        public static void Advance(RogueGame game, Actor owner)
        {
            if (!owner.Personality.HasOpenServiceAgreements) return;
            foreach (NpcServiceAgreement agreement in owner.Personality.ServiceAgreements)
            {
                if (agreement.Status != NpcServiceStatus.Accepted || agreement.Provider != owner.PersonalityIdentity ||
                    owner.Location.Map != agreement.Shelter.Map ||
                    !owner.Location.Map.GetTileAt(owner.Location.Position).IsInside ||
                    game.Rules.GridDistance(owner.Location.Position, agreement.Shelter.Position) > 2) continue;
                Actor patient = null;
                foreach (Actor actor in owner.Location.Map.Actors) if (actor.PersonalityIdentity == agreement.Patient) { patient = actor; break; }
                if (patient == null || patient.IsDead || !owner.Location.Map.GetTileAt(patient.Location.Position).IsInside ||
                    game.Rules.GridDistance(patient.Location.Position, agreement.Shelter.Position) > 2 ||
                    game.Rules.GridDistance(owner.Location.Position, patient.Location.Position) > 2 ||
                    NpcPlanExecution.Medicine(game, owner, owner.Location, true) == null) continue;
                if (owner.Personality.Intents.Any(i => !i.Finished && i.DefinitionId == "medical_aid" && i.TargetId == patient.PersonalityIdentity)) continue;
                NpcStorySystem.StartKnown(game.NpcContent, owner, new NpcKnownPerson { Id = patient.PersonalityIdentity,
                    Name = patient.UnmodifiedName, Place = patient.Location, SeenTurn = owner.Location.Map.LocalTime.TurnCounter },
                    game.NpcContent.Capability("medical_aid"), agreement.CauseId, agreement.StoryId);
            }
        }
        public static void Delivery(RogueGame game, SignificantEvent source)
        {
            if (source.Subject == null || source.Other == null || source.Subject.Personality == null ||
                !source.Subject.Personality.HasOpenServiceAgreements) return;
            NpcServiceAgreement agreement = source.Subject.Personality.ServiceAgreements.Find(a => a.Status == NpcServiceStatus.Accepted &&
                a.Provider == source.Subject.PersonalityIdentity && a.Patient == source.Other.PersonalityIdentity &&
                a.Shelter.Map == source.Map && source.Map.GetTileAt(source.Subject.Location.Position).IsInside &&
                source.Map.GetTileAt(source.Other.Location.Position).IsInside &&
                game.Rules.GridDistance(source.Subject.Location.Position, a.Shelter.Position) <= 2 &&
                game.Rules.GridDistance(source.Other.Location.Position, a.Shelter.Position) <= 2);
            if (agreement == null) return;
            PersonalitySystem.Report(game, new SignificantEvent("shelter_care_completed", source.Subject, source.Other,
                source.Map, source.Position, source.Turn, causeId: agreement.Id, storyId: agreement.StoryId));
        }
        public static void Expire(NpcContentCatalog catalog, Actor owner)
        {
            if (owner.Personality == null || !owner.Personality.HasOpenServiceAgreements || owner.IsSleeping || owner.IsDead) return;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            foreach (NpcServiceAgreement agreement in owner.Personality.ServiceAgreements)
            {
                if (turn < agreement.DueTurn) continue;
                if (agreement.Status == NpcServiceStatus.Offered) { owner.Personality.SetServiceStatus(agreement, NpcServiceStatus.Refused); continue; }
                if (agreement.Status != NpcServiceStatus.Accepted) continue;
                owner.Personality.SetServiceStatus(agreement, NpcServiceStatus.Failed);
                if (agreement.Patient != owner.PersonalityIdentity) continue;
                owner.Personality.Opinion(agreement.Provider, agreement.ProviderName).AdjustSocial(trust: -15, grievance: 12);
                long id = Session.Get.NextPersonalityEventId();
                var observed = new ObservedEvent("shelter_care_failed", turn, agreement.ProviderName, owner.UnmodifiedName,
                    true, subjectId: agreement.Provider, otherId: owner.PersonalityIdentity,
                    eventId: id, causeId: agreement.Id, storyId: agreement.StoryId);
                NpcRecordDescriptions.Annotate(observed, catalog.Event("shelter_care_failed"), catalog);
                owner.Personality.Remember(observed); Session.Get.ResidentRecords.Observe(owner, observed);
                owner.Personality.Knowledge.Learn(new NpcFact { EventId = id, CauseId = agreement.Id, Kind = "shelter_care_failed",
                    SubjectId = agreement.Provider, SubjectName = agreement.ProviderName, OtherId = owner.PersonalityIdentity,
                    OtherName = owner.UnmodifiedName, EventTurn = turn, LearnedTurn = turn, Confidence = 80,
                    Source = NpcKnowledgeSource.Inferred, SourceId = owner.PersonalityIdentity, Place = owner.Location,
                    StoryId = agreement.StoryId, NoSubjectLocation = true });
                MemoryDefinition definition = catalog.Personalities.Memory("shelter_care_failed");
                if (definition != null) NpcMemoryProcessor.Add(owner, definition, turn, agreement.ProviderName, null,
                    subjectId: agreement.Provider, relations: new NpcMemoryRelations { Person = agreement.Provider,
                        PersonName = agreement.ProviderName }, impact: -10);
            }
        }
    }
}
