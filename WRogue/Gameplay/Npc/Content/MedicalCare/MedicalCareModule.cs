using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class MedicalCareModule : INpcContentModule, INpcGoalSource
    {
        public const string RestoreHealthId = "restore_health";
        public string Id { get { return "medicalcare"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.PlanSeed(MedicalPlanOperators.Seed);
            catalog.OperatorSource(new NpcOperatorSource("medicine.acquire", c => (ulong)NpcPlanFact.Medicine, MedicalPlanOperators.Acquisition));
            catalog.OperatorSource(new NpcOperatorSource("medicine.heal", c => (ulong)NpcPlanFact.Healthy, MedicalPlanOperators.Healing, c => (ulong)NpcPlanFact.Medicine));
            catalog.OperatorSource(new NpcOperatorSource("medicine.aid", c => (ulong)NpcPlanFact.Helped, MedicalPlanOperators.Aid, c => (ulong)NpcPlanFact.Medicine));
            catalog.OperatorSource(new NpcOperatorSource("medicine.deliver", c => (ulong)NpcPlanFact.Delivered, MedicalPlanOperators.Aid,
                c => (ulong)NpcPlanFact.Medicine, d => d.Resource == "medicine"));
            catalog.Event(new NpcEventDefinition("medicine_cache", describeReport: f => "there was medicine"));
            catalog.OnReport("medicine_cache", c => c.RememberPlace("medicine"));
            catalog.Resource(new NpcResourceDefinition("medicine", (g, a) => a.HitPoints < g.Rules.ActorMaxHPs(a)));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Recovery", "Recover health", NpcGoalValue.Recovery, m => 160 + m.Supplies - m.Courage / 2, true));
            catalog.Value(new NpcValueDefinition("MedicalCare", "Meet a person's medical need", NpcGoalValue.MedicalCare, m => 15 + m.Compassion + m.Attachment / 3 + m.Feeling / 3 + m.CommunityCare, true));
            var recover = new NpcIntentDefinition(RestoreHealthId, "Recover health",
            NpcIntentMethod.RestoreHealth, 40, 20, 180, 180, 0);

            recover.Result = (c, g) => (ulong)(NpcPlanFact.Healthy);
            recover.Resource = "medicine";
            catalog.Capability(recover);
            var medicalaid = new NpcIntentDefinition("medical_aid", "Meet a person's medical need", NpcIntentMethod.MedicalAid, 25, 20, 180, 60, 1);

            medicalaid.Result = (c, g) => (ulong)(NpcPlanFact.Helped);
            medicalaid.Resource = "medicine";
            catalog.Capability(medicalaid);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            if (owner.HitPoints < maxHP || context.Pending(NpcGoalValue.Recovery, self.Id))
                offers.Add(self, NpcGoalValue.Recovery, context.Catalog.Capability(RestoreHealthId), owner.HitPoints, maxHP,
                    Math.Max(0, maxHP - owner.HitPoints) * 100 / Math.Max(1, maxHP), 100);
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead || person.Hostile) continue;
                if ((person.MedicalNeed > 0 || context.Pending(NpcGoalValue.MedicalCare, person.Id)) && turn - person.MedicalTurn <= 60)
                    offers.Add(person, NpcGoalValue.MedicalCare, context.Catalog.Capability("medical_aid"),
                        100 - person.MedicalNeed, 100, person.MedicalNeed, person.MedicalConfidence, person.MedicalCause, person.MedicalStory, resource: "medicine");
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Items, PerceiveItems);
            catalog.Perception(NpcPerceptionKind.Person, c => NpcMedicalObservation.Perceive(c.Game, c.Owner, c.Person, c.Owner.Personality.Knowledge.Person(c.Person.PersonalityIdentity), c.Turn));
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("medicine.ask", NpcPlanAction.AskMedicine, c => NpcMedicineActions.Ask(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("medicine.give", NpcPlanAction.GiveMedicine, c => NpcMedicineActions.Give(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("medicine.treat", NpcPlanAction.TreatPerson, c => NpcMedicineActions.Treat(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("medicine.trade", NpcPlanAction.BarterMedicine, c => new ActionNpcMedicineTrade(c.Owner, c.Game, c.Goal, c.Step, c.Target)));
            catalog.Operator(new NpcOperatorDefinition("medicine.take", NpcPlanAction.PickupMedicine, c => NpcMedicineActions.Take(new NpcActionContext(c)), "medicine", NpcContentActions.MedicineUnavailable));
            catalog.Operator(new NpcOperatorDefinition("medicine.use", NpcPlanAction.UseMedicine, c => NpcMedicineActions.Use(new NpcActionContext(c))));
            NpcMemoryContent.Received(catalog, "medical_aid", "Received medicine or treatment", "shared_medicine", 8, "protector");
            NpcMemoryContent.Received(catalog, "medical_treatment", "Someone treated my wounds", "treated_person", 8, "protector");
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("shared_medicine", c => NpcReputation.Help(c));
            catalog.OnReport("requested_medicine", c => {
                NpcKnownPerson person = c.Listener.Personality.Knowledge.Person(c.Fact.SubjectId); NpcFact fact = c.Fact;
                if (person != null && (fact.EventTurn > person.MedicalTurn || fact.EventTurn == person.MedicalTurn && fact.Confidence > person.MedicalConfidence))
                { person.MedicalNeed = 100; person.MedicalConfidence = fact.Confidence; person.MedicalTurn = fact.EventTurn; person.MedicalCause = fact.EventId; person.MedicalStory = fact.StoryId; }
            });
            catalog.Event(new NpcEventDefinition("medicine_acquired", NpcRecordCategory.Life, false, e => (e.Subject ?? "Someone") + " acquired medicine to treat their wounds.", null));
            catalog.Event(new NpcEventDefinition("treated_wounds", NpcRecordCategory.Life, false, e => (e.Subject ?? "Someone") + " treated their wounds with real medicine.", null));
            catalog.Event(new NpcEventDefinition("requested_medicine", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " for medicine.", f => f.ReportSubject + " asked " + (f.ReportOther == null ? "" : f.ReportOther + " ") + "for medicine") { AudibleReport = true, PlayerReply = new NpcPlayerReply("medicine", "medicine_promised", "request_refused", "Yes, I'll bring you medicine.", "No, I can't help with medicine.") });
            catalog.Event(new NpcEventDefinition("medicine_offered", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " offered medicine in exchange for supplies to " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("bartered_medicine", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " obtained medicine by trading with " + (e.Other ?? "someone") + ".", null) { ReportActorRole = NpcReportActorRole.Both });
            catalog.Event(new NpcEventDefinition("shared_medicine", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " gave medicine to " + (e.Other ?? "someone") + ".", null) { SelfReportTone = NpcSelfReportTone.Helpful });
            catalog.Event(new NpcEventDefinition("treated_person", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " treated " + (e.Other ?? "someone") + "'s wounds.", null));
            catalog.On("medicine_offered", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("requested_medicine", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_medicine", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("treated_person", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            if (source.Kind == "medicine_offered" && source.Other == owner)
                foreach (NpcIntent goal in owner.Personality.Intents)
                    if (!goal.Finished && goal.Plan != null && (goal.DefinitionId == RestoreHealthId || goal.Generated != null && goal.Generated.Resource == "medicine"))
                    { goal.Plan.Invalidate(); goal.NextAttempt = source.Turn + 1; goal.Plan.NextPlanningTurn = source.Turn + 1; }
        }
        static void OnRelationships(NpcObservation observation)
        {
            RogueGame game = observation.Game; Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = source.Subject == null ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            NpcKnownPerson other = source.Other == null ? null : knowledge.Person(source.Other.PersonalityIdentity);
            if (source.Kind == "requested_medicine" && subject != null)
            { subject.MedicalNeed = 100; subject.MedicalConfidence = 100; subject.MedicalTurn = source.Turn; subject.MedicalCause = source.Id; subject.MedicalStory = source.StoryId; }
            if ((source.Kind == "shared_medicine" || source.Kind == "treated_person") && other != null)
            { other.MedicalNeed = source.Kind == "shared_medicine" ? 0 : Math.Max(0, game.Rules.ActorMaxHPs(source.Other) - source.Other.HitPoints) * 100 / game.Rules.ActorMaxHPs(source.Other);
                other.MedicalTurn = source.Turn; other.Wounds = Math.Max(0, game.Rules.ActorMaxHPs(source.Other) - source.Other.HitPoints) * 100 / game.Rules.ActorMaxHPs(source.Other); owner.Personality.Knowledge.Revision++; }
            if ((source.Kind == "shared_medicine" || source.Kind == "treated_person") && source.Other == owner && subject != null)
            { owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: 5, attachment: 4, debt: 10); subject.SocialCause = source.Id;
                owner.Personality.Attach(new NpcAttachment { Kind = "person", Person = subject.Id, Name = subject.Name, Weight = 20, CauseId = source.Id }); }
        }
    }
}
