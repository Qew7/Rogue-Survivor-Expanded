using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class PromisesModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "promises"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Clock(NpcClockPhase.MapTurn, c => NpcPromises.Expire(c.Owner, c.Game.NpcContent));
            catalog.Clock(NpcClockPhase.BeforeDecision, c => NpcPromises.Expire(c.Owner, c.Game.NpcContent));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Commitment", "Fulfil an outstanding promise", NpcGoalValue.Commitment, m => 70 + m.Law + m.Compassion + m.Feeling / 4, false));
            var promise = new NpcIntentDefinition("fulfil_promise", "Fulfil a promise", NpcIntentMethod.FulfilPromise, 35, 20, 180, 0, 1);
            promise.BuildPlan = d => { PromisePlanOperators.Build(d); };
            promise.Result = (c, g) => (ulong)(NpcPlanFact.Delivered);
            catalog.Capability(promise);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            if (owner.Personality.HasCommitments) foreach (NpcCommitment promise in owner.Personality.Commitments)
            {
                if (promise.Promisor != self.Id || promise.Status != NpcCommitmentStatus.Active || turn >= promise.DueTurn) continue;
                NpcKnownPerson recipient = context.FindPerson(p => p.Id == promise.Beneficiary);
                if (recipient == null || recipient.Hostile) continue;
                offers.Add(recipient, NpcGoalValue.Commitment, context.Catalog.Capability("fulfil_promise"),
                    0, promise.Units, 100, 100, promise.Id, promise.StoryId, resource: promise.Resource, obligation: promise.Id);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            RegisterEvents(catalog);
            catalog.Trait(new TraitDefinition("reliable", "Reliable", true, false, "honest",
                new TraitEffect(DecisionKind.Law, 12), new TraitEffect(DecisionKind.Compassion, 8)));
            catalog.Trait(new TraitDefinition("disillusioned", "Disillusioned", true, false, "trusting",
                new TraitEffect(DecisionKind.Group, -12), new TraitEffect(DecisionKind.Trade, -8)));
            NpcMemoryContent.Received(catalog, "promise_received", "Received a promise of assistance", "food_promised", 2, null);
            NpcMemoryContent.Received(catalog, "medicine_promise_received", "Received a promise of medicine", "medicine_promised", 2, null);
            NpcMemoryContent.Received(catalog, "promise_kept", "Someone kept their promise", "promise_kept", 12, null);
            catalog.Memory(new MemoryDefinition("kept_my_word", "Fulfilled my promise", 2, 5,
                new[] { new MemoryTrigger("promise_kept", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "reliable", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC)).Relate(MemoryRelationRole.Other, 2), false);
            NpcMemoryContent.Received(catalog, "promise_broken", "A promise was not fulfilled by its deadline", "promise_broken", -15, "disillusioned");
            NpcMemoryContent.Received(catalog, "promise_released", "Released from a promise", "promise_released", 3, null);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.AfterEvent("shared_food", (g, e) => { if (e.Subject != null && e.Subject.Personality != null && e.Other != null) NpcPromises.Delivery(g, e.Subject, e.Other, "food", e.Id, e.StoryId); });
            catalog.AfterEvent("shared_medicine", (g, e) => { if (e.Subject != null && e.Subject.Personality != null && e.Other != null) NpcPromises.Delivery(g, e.Subject, e.Other, "medicine", e.Id, e.StoryId); });
            catalog.OnReport("promise_kept", c => NpcReputation.Reputation(c, true, false, false));
            catalog.OnReport("promise_broken", c => NpcReputation.Reputation(c, false, true, false));
            catalog.Event(new NpcEventDefinition("food_promised", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " promised to bring food to " + (e.Other ?? "someone") + " within 180 turns.", null));
            catalog.Event(new NpcEventDefinition("medicine_promised", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " promised to bring medicine to " + (e.Other ?? "someone") + " within 180 turns.", null));
            catalog.Event(new NpcEventDefinition("promise_kept", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " fulfilled their promise to " + (e.Other ?? "someone") + ".", f => f.SubjectName + " kept a promise") { CanWitness = (a, e) => a.Personality != null && a.Personality.Knowledge.Facts.Exists(f => f.EventId == e.CauseId && (f.Kind == "food_promised" || f.Kind == "medicine_promised")) });
            catalog.Event(new NpcEventDefinition("promise_broken", NpcRecordCategory.Help, false, e => (e.Other ?? "someone") + " concluded that " + (e.Subject ?? "Someone") + "'s promise was overdue.", f => f.SubjectName + " did not meet a promise's deadline", isPrivate: true) { PrivateAudience = e => e.Other });
            catalog.Event(new NpcEventDefinition("promise_released", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " explicitly released " + (e.Other ?? "someone") + " from a promise.", null));
            catalog.On("requested_food", NpcObservationPhase.Responses, o => NpcPromises.PrepareReply(o.Game, o.Owner, o.Source));
            catalog.On("requested_medicine", NpcObservationPhase.Responses, o => NpcPromises.PrepareReply(o.Game, o.Owner, o.Source));
            catalog.On("food_promised", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("medicine_promised", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("promise_kept", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("promise_released", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_given", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_food", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_medicine", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnRelationships(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = source.Subject == null ? null : knowledge.Person(source.Subject.PersonalityIdentity);
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
            if (source.Kind == "promise_kept" && source.Other == owner && subject != null)
            { owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: 15, attachment: 5); subject.SocialCause = source.Id; }
            if (source.Kind == "promise_released" && (owner == source.Subject || owner == source.Other))
                foreach (NpcCommitment promise in owner.Personality.Commitments)
                    if (promise.Status == NpcCommitmentStatus.Active && promise.Id == source.CauseId && promise.Beneficiary == source.Subject.PersonalityIdentity && promise.Promisor == source.Other.PersonalityIdentity)
                    {
                        promise.Status = NpcCommitmentStatus.Released;
                        foreach (NpcIntent intent in owner.Personality.Intents) if (!intent.Finished && intent.Generated != null && intent.Generated.ObligationId == promise.Id)
                            NpcIntentSystem.Finish(owner, intent, NpcIntentStatus.Abandoned, "recipient explicitly released the promise");
                    }
            if ((source.Kind == "shared_food" || source.Kind == "shared_medicine") && source.Other == owner && PersonalitySystem.Bias(owner, DecisionKind.Compassion) > 0)
                NpcPromises.ReleaseUnneeded(game, owner, source);
        }
    }
}
