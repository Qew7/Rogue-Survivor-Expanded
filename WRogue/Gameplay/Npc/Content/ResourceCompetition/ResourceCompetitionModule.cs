using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ResourceCompetitionModule : INpcContentModule, INpcActionGuard
    {
        public string Id { get { return "resource-competition"; } }
        public void Register(NpcCatalogBuilder catalog) { catalog.AfterEvent("resource_yielded", (g, e) => { if (e.Subject != null) Session.Get.NpcDirector.ReleaseOwned(e.ResourcePlace, e.Subject.PersonalityIdentity); }); catalog.ActionGuard(this); RegisterContent(catalog); }
        public NpcActionAccess Check(NpcExecutionContext context, NpcOperatorDefinition definition)
        {
            if (definition.Resource == null) return new NpcActionAccess(true);
            Guid id = Session.Get.NpcDirector.ReservationOwner(context.Step.Place, context.Goal.StoryId);
            if (id == Guid.Empty || id == context.Owner.PersonalityIdentity) return new NpcActionAccess(Session.Get.NpcDirector.Reserve(context.Goal.StoryId, context.Step.Place));
            Actor holder = NpcIntentSystem.VisibleTarget(context.Visible, id);
            if (holder == null) return new NpcActionAccess(false);
            NpcResourceDispute dispute = context.Owner.Personality.Dispute(context.Step.Place, id);
            if (dispute != null && dispute.Response == "resource_refused" &&
                PersonalitySystem.Bias(context.Owner, DecisionKind.Law) + PersonalitySystem.Bias(context.Owner, DecisionKind.Courage) / 2 < 0)
            return new NpcActionAccess(true);
            var action = new ActionNpcResourceConflict(context.Owner, context.Game, context.Goal, context.Step, holder, definition.Resource);
            return new NpcActionAccess(false, action.IsLegal() ? action : null);
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            RegisterEvents(catalog);
            NpcMemoryContent.Received(catalog, "resource_rivalry", "Disputed scarce supplies", "resource_contested", -4, "mistrustful");
            NpcMemoryContent.Received(catalog, "resource_concession", "Someone yielded scarce supplies", "resource_yielded", 6, "protector");
            NpcMemoryContent.Received(catalog, "resource_taken", "Someone took disputed supplies", "contested_taken", -12, "mistrustful");
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("contested_taken", c => NpcReputation.Reputation(c, false, false, true));
            catalog.Event(new NpcEventDefinition("resource_contested", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to yield disputed supplies.", null));
            catalog.Event(new NpcEventDefinition("resource_yielded", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " yielded disputed supplies to " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("resource_refused", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " refused to yield supplies to " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("contested_taken", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " took supplies despite " + (e.Other ?? "someone") + "'s refusal.", f => f.SubjectName + " took disputed supplies"));
            catalog.On("contested_taken", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_contested", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_refused", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_yielded", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnRelationships(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = source.Subject == null ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            if (source.Kind == "contested_taken" && owner == source.Other && subject != null)
            { subject.Violation = 100; subject.ViolationCause = source.Id; subject.ViolationTurn = source.Turn; subject.ViolationConfidence = 100; }
            if (source.Kind == "resource_contested" || source.Kind == "resource_yielded" || source.Kind == "resource_refused" || source.Kind == "contested_taken")
                NpcResourceCompetition.ObserveDispute(game, owner, source, direct);
        }
    }
}
