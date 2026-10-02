using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class SafetyModule : INpcContentModule, INpcGoalSource
    {
        public string Id { get { return "safety"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.OperatorSource(new NpcOperatorSource("safety.retreat", c => (ulong)NpcPlanFact.Safe, SafetyPlanOperators.Avoid));
            catalog.OperatorSource(new NpcOperatorSource("group.leave", c => (ulong)NpcPlanFact.Left, SafetyPlanOperators.Leave));
            catalog.OperatorSource(new NpcOperatorSource("shelter.reach", c => (ulong)NpcPlanFact.Sheltered, SafetyPlanOperators.Shelter));
            catalog.GoalSource(this);
            catalog.Value(new NpcValueDefinition("Safety", "Reach safety", NpcGoalValue.Safety, m => 20 - m.Courage + m.Fear / 2, false));
            catalog.Value(new NpcValueDefinition("Autonomy", "Leave an unsafe group", NpcGoalValue.Autonomy, m => 40 - m.Group - m.Courage / 2 - Math.Min(2 * Rules.TRUST_TRUSTING_THRESHOLD, Math.Max(0, m.Owner.TrustInLeader)) * 30 / Rules.TRUST_TRUSTING_THRESHOLD, false));
            var avoid = new NpcIntentDefinition("avoid_reported_threat", "Avoid a reported aggressor",
            NpcIntentMethod.AvoidPerson, 20, 35, 180, 180, -1, new NpcIntentWeight(DecisionKind.Courage, -1));

            avoid.Result = (c, g) => (ulong)(NpcPlanFact.Safe);
            avoid.AllowHostile = true;
            avoid.SocialPriority = r => r.Fear / 2;
            avoid.DirectAction = NpcSafetyActions.Confirm;
            catalog.Capability(avoid);
            var leave = new NpcIntentDefinition("leave_unsafe_group", "Leave an unsafe leader",
            NpcIntentMethod.LeaveGroup, 40, 55, WorldTime.TURNS_PER_DAY, WorldTime.TURNS_PER_DAY, -1,
            new NpcIntentWeight(DecisionKind.Group, -1), new NpcIntentWeight(DecisionKind.Courage, -1, 2));

            leave.Result = (c, g) => (ulong)(NpcPlanFact.Left);
            leave.Departure = true; leave.AllowHostile = true;
            leave.Assess = (g, a, i) => a.Leader == null || a.Leader.PersonalityIdentity != i.TargetId ? new NpcIntentOutcome(NpcIntentStatus.Abandoned, "group membership changed") : null;
            leave.DirectAction = NpcSafetyActions.Leave;
            catalog.Capability(leave);
            RegisterContent(catalog);
        }
        public void Evaluate(NpcGoalContext context, NpcGoalOffers offers)
        {
            Actor owner = context.Owner; NpcKnownPerson self = context.Self;
            IList<NpcKnownPerson> people = context.People; int turn = context.Turn, maxHP = context.MaxHP;
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead) continue;
                int danger = turn - person.ThreatTurn <= 180 ? person.Danger : 0;
                int violation = person.Violation;
                bool near = person.Place.Map == owner.Location.Map && NpcGoalContext.Distance(owner.Location, person.Place) < 5;
                if (danger > 0 && (near || context.Pending(NpcGoalValue.Safety, person.Id)))
                    offers.Add(person, NpcGoalValue.Safety, context.Catalog.Capability("avoid_reported_threat"), danger, 0, danger, person.ThreatConfidence, person.ThreatCause);
                if (owner.Leader != null && owner.Leader.PersonalityIdentity == person.Id && danger > 0)
                    offers.Add(person, NpcGoalValue.Autonomy, context.Catalog.Capability("leave_unsafe_group"), danger, 0, danger, person.ThreatConfidence, person.ThreatCause);
            }
        }
        void RegisterContent(NpcCatalogBuilder catalog)
        {
            catalog.Perception(NpcPerceptionKind.Person, c => {
                NpcKnownPerson known = c.Owner.Personality.Knowledge.Person(c.Person.PersonalityIdentity);
                if (!known.Hostile) return;
                if (known.Danger < 70) { known.Danger = 70; c.Owner.Personality.Knowledge.Revision++; }
                known.ThreatTurn = c.Turn; known.ThreatConfidence = 100;
            });
            RegisterEvents(catalog);
            catalog.Operator(new NpcOperatorDefinition("safety.confirm", NpcPlanAction.ConfirmSafety, c => NpcSafetyActions.Confirm(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("shelter.enter", NpcPlanAction.EnterShelter, c => NpcSafetyActions.Shelter(new NpcActionContext(c))));
            catalog.Operator(new NpcOperatorDefinition("group.leave", NpcPlanAction.LeaveGroup, c => NpcSafetyActions.Leave(new NpcActionContext(c.Game, c.Owner, c.Goal, c.Owner.Leader))));
            catalog.Memory(new MemoryDefinition("left_unsafe_group", "Left an unsafe group", 2, 6,
                new[] { new MemoryTrigger("left_group", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "hermit", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Other, 0, MemoryRelationRole.Other), false);
            catalog.Memory(new MemoryDefinition("companion_departed", "A companion chose to leave", 2, 6,
                new[] { new MemoryTrigger("left_group", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Subject, -5), false);
            catalog.Memory(new MemoryDefinition("frightened_escape", "Escaped a mortal threat", 2, 5,
                new[] { new MemoryTrigger("fled_in_fear", (a, e) => a == e.Subject && !a.IsPlayer) },
                new MemoryOutcome((a, m) => a.Personality.HasTrait("fearful"), "panic_attacks", null),
                new MemoryOutcome((a, m) => a.Personality.HasTrait("timid"), "traumatized", null),
                new MemoryOutcome((a, m) => a.Personality.HasTrait("brave"), "hardened", null),
                new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Other, -5), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("attack", c => { HearNeed(c); NpcReputation.Attack(c); });
            catalog.OnReport("murder", c => { HearNeed(c); NpcReputation.Attack(c); });
            catalog.OnReport("fled_in_fear", HearNeed);
            catalog.Event(new NpcEventDefinition("left_group", NpcRecordCategory.Encounters, false, e => (e.Subject ?? "Someone") + " chose to leave " + (e.Other ?? "someone") + "'s group.", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("withdrew", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " withdrew from the last reported danger location.", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("fled_in_fear", NpcRecordCategory.Combat | NpcRecordCategory.Life, true,
                e => (e.Subject ?? "Someone") + " fled in fear of " + (e.Other ?? "a threat") + ".",
                f => (f.ReportSubject ?? "someone") + " fled in fear of " + (f.ReportOther ?? "a threat"),
                NpcEventFields.Subject | NpcEventFields.Other));
            catalog.On("attack", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("murder", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("fled_in_fear", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("attack", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("murder", NpcObservationPhase.Relationships, OnRelationships);
        }
        static void OnKnowledge(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            bool seesSubject = observation.SeesSubject, seesOther = observation.SeesOther;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            NpcKnownPerson other = !seesOther ? null : knowledge.Person(source.Other.PersonalityIdentity);
            int confidence = direct ? 100 : 90;
            if ((source.Kind == "attack" || source.Kind == "murder") && other != null && other.Id != owner.PersonalityIdentity)
            {
                other.Danger = 100; other.Violation = 100; other.ThreatTurn = source.Turn;
                other.ThreatConfidence = confidence; other.ThreatCause = source.Id; knowledge.Revision++;
                other.ViolationTurn = source.Turn; other.ViolationConfidence = confidence; other.ViolationCause = source.Id;
            }
            if (source.Kind == "fled_in_fear" && other != null && other.Id != owner.PersonalityIdentity)
            { other.Danger = Math.Max(other.Danger, 70); other.ThreatTurn = source.Turn;
                other.ThreatConfidence = confidence; other.ThreatCause = source.Id; knowledge.Revision++; }
        }
        static void OnRelationships(NpcObservation observation)
        {
            Actor owner = observation.Owner;
            bool seesOther = observation.SeesOther;
            SignificantEvent source = observation.Source; bool direct = observation.Direct;
            PersonalityState state = owner.Personality; NpcKnowledge knowledge = state.Knowledge;
            if (seesOther && (source.Kind == "attack" || source.Kind == "murder") && source.Other != null && source.Other != owner)
                owner.Personality.Opinion(source.Other.PersonalityIdentity, source.Other.UnmodifiedName).AdjustSocial(
                    fear: source.Subject == owner ? 15 : 5, grievance: source.Subject == owner ? 20 : 3);
        }
    }
}
