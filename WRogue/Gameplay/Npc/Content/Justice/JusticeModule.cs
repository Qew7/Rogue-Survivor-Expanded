using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class JusticeModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "justice"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.OperatorSource(new NpcOperatorSource("person.boundary", c => (ulong)NpcPlanFact.Warned, d => CompanionPlanOperators.Build(d, true)));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Justice", "Communicate a boundary", NpcGoalValue.Justice, m => 15 + m.Courage + m.Law + m.Grievance / 2, false));
            catalog.Value(new NpcValueDefinition("Restitution", "Replace supplies lost through my actions", NpcGoalValue.Restitution, m => 15 + m.Law + m.Compassion + m.Feeling / 4, true) { AfterDelivery = c => {
                NpcKnownPerson person = c.Owner.Personality.Knowledge.Person(c.Target.PersonalityIdentity);
                if (person != null) person.LossUnits = Math.Max(0, person.LossUnits - 1);
                c.Publish("restitution_given", c.Target); return person != null && person.LossUnits > 0;
            } });
            var confront = new NpcIntentDefinition("confront_reported_aggressor", "Confront a reported aggressor",
            NpcIntentMethod.ConfrontPerson, 15, 35, 180, 180, -1, new NpcIntentWeight(DecisionKind.Courage, 1), new NpcIntentWeight(DecisionKind.Law, 1));

            confront.Result = (c, g) => (ulong)(NpcPlanFact.Warned);
            confront.SocialPriority = r => r.Grievance / 2;
            confront.AllowQuestions = true;
            confront.DirectAction = NpcContactActions.Warn;
            catalog.Capability(confront);
            var restitution = new NpcIntentDefinition("restore_property", "Replace lost supplies", NpcIntentMethod.RestoreProperty, 30, 20, 180, 180, 1);

            restitution.Result = (c, g) => (ulong)(NpcPlanFact.Delivered);
            restitution.Resource = "food";
            restitution.DirectAction = NpcFoodActions.Give;
            catalog.Capability(restitution);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead) continue;
                int violation = person.Violation;
                if (!person.Hostile && violation > 0)
                    offers.Add(person, NpcGoalValue.Justice, context.Catalog.Capability("confront_reported_aggressor"), violation, 0, violation,
                        person.ViolationConfidence == 0 ? person.ThreatConfidence : person.ViolationConfidence,
                        person.ViolationCause == 0 ? person.ThreatCause : person.ViolationCause);
                if (person.Hostile) continue;
                if (person.LossUnits > 0 || context.Pending(NpcGoalValue.Restitution, person.Id))
                    offers.Add(person, NpcGoalValue.Restitution, context.Catalog.Capability("restore_property"),
                        person.LossUnits, 0, Math.Min(100, person.LossUnits * 40), 100, person.LossCause, resource: "food", objectPlace: person.LossPlace);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("boundary.warn", NpcPlanAction.Warn, c => NpcContactActions.Warn(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("restitution.demand", NpcPlanAction.DemandRestitution, c => new ActionNpcRestitutionDemand(c.Owner, c.Game, c.Goal, c.Step, c.Target)));
            NpcMemoryContent.Received(catalog, "boundary_accepted", "Someone accepted a boundary", "boundary_accepted", 4, null);
            NpcMemoryContent.Received(catalog, "boundary_defied", "Someone rejected a boundary", "boundary_defied", -8, "mistrustful");
            NpcMemoryContent.Received(catalog, "restitution", "Someone replaced lost supplies", "restitution_given", 10, null);
            NpcMemoryContent.Received(catalog, "restitution_refused", "A demand for compensation was refused", "restitution_refused", -8, "mistrustful");
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("base_theft", c => NpcReputation.Reputation(c, false, false, true));
            catalog.OnReport("boundary_defied", c => NpcReputation.Reputation(c, false, false, true));
            catalog.Event(new NpcEventDefinition("confronted", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " warned " + (e.Other ?? "someone") + " about known misconduct.", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("boundary_accepted", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " accepted " + (e.Other ?? "someone") + "'s boundary.", null));
            catalog.Event(new NpcEventDefinition("boundary_defied", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " rejected " + (e.Other ?? "someone") + "'s boundary.", f => f.ReportSubject + " rejected " + (f.ReportOther == null ? "a" : f.ReportOther + "'s") + " warning") { SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.Event(new NpcEventDefinition("base_theft", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " stole from " + (e.Other ?? "someone") + "'s base.", f => f.ReportSubject + " stole supplies from " + (f.ReportOther ?? "someone") + "'s base") { SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.Event(new NpcEventDefinition("restitution_given", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " replaced supplies lost by " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("restitution_requested", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to compensate lost supplies.", null) { AudibleReport = true, PlayerReply = new NpcPlayerReply("replacement supplies", "food_promised", "restitution_refused", "Yes, I'll replace your supplies.", "No, I won't replace them.") });
            catalog.Event(new NpcEventDefinition("restitution_refused", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " refused " + (e.Other ?? "someone") + "'s demand for compensation.", null) { SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.On("base_theft", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("confronted", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("base_theft", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("boundary_accepted", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("boundary_defied", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("confronted", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_given", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_refused", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_requested", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_food", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = !seesSubject ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            NpcKnownPerson other = !seesOther ? null : knowledge.Person(source.Other.PersonalityIdentity);
            int confidence = direct ? 100 : 90;
            if (source.Kind == "base_theft" && subject != null && subject.Id != owner.PersonalityIdentity)
            {
                subject.Violation = 100; subject.ThreatTurn = source.Turn; subject.ThreatConfidence = confidence;
                subject.ThreatCause = source.Id; knowledge.Revision++;
                subject.ViolationTurn = source.Turn; subject.ViolationConfidence = confidence; subject.ViolationCause = source.Id;
            }
            if (source.Kind == "confronted" && source.Subject == owner && other != null)
            { other.Violation = 0; other.AcknowledgedViolation = source.CauseId; knowledge.Revision++; }
        }
        static void OnRelationships(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson subject = source.Subject == null ? null : knowledge.Person(source.Subject.PersonalityIdentity);
            NpcKnownPerson other = source.Other == null ? null : knowledge.Person(source.Other.PersonalityIdentity);
            if (source.Kind == "restitution_given" && subject != null && owner == source.Other)
            {
                foreach (NpcAttachment home in owner.Personality.Attachments)
                    if (home.Kind == "place" && home.Place == source.ResourcePlace && home.Resource == "food" && home.Person == source.Subject.PersonalityIdentity) home.MissingUnits = Math.Max(0, home.MissingUnits - 1);
            }
            if (source.Kind == "shared_food" && source.Subject != null && source.Subject.IsPlayer && owner == source.Other)
                foreach (NpcAttachment home in owner.Personality.Attachments)
                    if (home.Kind == "place" && home.Resource == "food" && home.Person == source.Subject.PersonalityIdentity && home.MissingUnits > 0)
                    { home.MissingUnits = Math.Max(0, home.MissingUnits - Math.Max(1, source.Units)); break; }
            if (source.Kind == "base_theft" && other != null && source.Subject == owner && source.Resource == "food")
            { other.LossUnits += Math.Max(1, source.Units); other.LossCause = source.Id; other.LossPlace = new Location(source.Map, source.Position); }
            if ((source.Kind == "boundary_accepted" || source.Kind == "boundary_defied" || source.Kind == "restitution_refused") && owner == source.Other && subject != null)
            {
                subject.Violation = source.Kind == "boundary_accepted" ? 0 : 100;
                subject.ViolationCause = source.Id; subject.ViolationTurn = source.Turn; subject.ViolationConfidence = 100;
                owner.Personality.Opinion(subject.Id, subject.Name).AdjustSocial(trust: source.Kind == "boundary_accepted" ? 5 : -10, grievance: source.Kind == "boundary_accepted" ? -5 : 10);
            }
            if (source.Kind == "confronted" && source.Other == owner && NpcIntentSystem.Enabled(owner))
                NpcReplies.Reply(owner, source.Subject, source, PersonalitySystem.Bias(owner, DecisionKind.Law) + PersonalitySystem.Bias(owner, DecisionKind.Compassion) >= 0 ?
                    "boundary_accepted" : "boundary_defied", "I understand. I'll respect that.", "You don't decide what I do.");
            if (source.Kind == "restitution_requested" && source.Other == owner && subject != null && NpcIntentSystem.Enabled(owner))
            {
                bool accept = PersonalitySystem.Bias(owner, DecisionKind.Law) + PersonalitySystem.Bias(owner, DecisionKind.Compassion) >= 10;
                if (accept && subject.LossCause > 0) subject.LossUnits = Math.Max(subject.LossUnits, source.Units);
                NpcReplies.Reply(owner, source.Subject, source, accept ? "boundary_accepted" : "restitution_refused", "I'll try to replace what was lost.", "I won't replace your supplies.");
            }
        }
    }
}
