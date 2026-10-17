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
            catalog.Value(new NpcValueDefinition("Justice", "Communicate a boundary", NpcGoalValue.Justice, m => 15 + m.Courage + m.Law + m.Grievance / 2 - m.Patience / 2, false));
            catalog.Value(new NpcValueDefinition("Restitution", "Replace supplies lost through my actions", NpcGoalValue.Restitution, m => 15 + m.Law + m.Compassion + m.Feeling / 4, true) { AfterDelivery = c => {
                NpcKnownPerson person = c.Owner.Personality.Knowledge.Person(c.Target.PersonalityIdentity);
                if (person != null) person.LossUnits = Math.Max(0, person.LossUnits - 1);
                c.Publish("restitution_given", c.Target); return person != null && person.LossUnits > 0;
            } });
            var confront = new NpcIntentDefinition("confront_reported_aggressor", "Confront a reported aggressor",
            NpcIntentMethod.ConfrontPerson, 15, 35, 180, 180, -1, new NpcIntentWeight(NpcPersonalityAxis.Courage, 1), new NpcIntentWeight(NpcPersonalityAxis.Law, 1));

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
            catalog.Operator(new NpcOperatorDefinition("boundary.warn", NpcPlanAction.Warn, c => NpcContactActions.Warn(new NpcActionContext(c)), archiveText: step => "warn someone"));
            catalog.Operator(new NpcOperatorDefinition("restitution.demand", NpcPlanAction.DemandRestitution, c => new ActionNpcRestitutionDemand(c.Owner, c.Game, c.Goal, c.Step, c.Target), archiveText: step => "demand restitution"));
            NpcMemoryContent.Received(catalog, "boundary_accepted", "Someone accepted a boundary", "boundary_accepted", 4, null);
            NpcMemoryContent.Received(catalog, "boundary_defied", "Someone rejected a boundary", "boundary_defied", -8, "mistrustful");
            NpcMemoryContent.Received(catalog, "restitution", "Someone replaced lost supplies", "restitution_given", 10, null);
            NpcMemoryContent.Received(catalog, "restitution_refused", "A demand for compensation was refused", "restitution_refused", -8, "mistrustful");
            catalog.Memory(new MemoryDefinition("boundary_respected", "My boundary was respected", 2, 2,
                new[] { new MemoryTrigger("boundary_accepted", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "patient", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, 6).ChangeDisposition(NpcPersonalityAxis.Patience, 8), false);
            catalog.Memory(new MemoryDefinition("boundary_conceded", "I respected someone's boundary", 2, 2,
                new[] { new MemoryTrigger("boundary_accepted", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "patient", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Other, 3).ChangeDisposition(NpcPersonalityAxis.Patience, 6), false);
            catalog.Memory(new MemoryDefinition("boundary_respect_witnessed", "I saw a warning respected", 2, 2,
                new[] { new MemoryTrigger("boundary_accepted", (a, e) => a != e.Subject && a != e.Other) },
                new MemoryOutcome(null, "patient", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, 0).ChangeDisposition(NpcPersonalityAxis.Patience, 3), false);
            catalog.Memory(new MemoryDefinition("boundary_ignored", "My boundary was ignored", 2, 2,
                new[] { new MemoryTrigger("boundary_defied", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "hotheaded", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -8).ChangeDisposition(NpcPersonalityAxis.Patience, -8), false);
            catalog.Memory(new MemoryDefinition("boundary_rejected", "I rejected someone's boundary", 2, 2,
                new[] { new MemoryTrigger("boundary_defied", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "hotheaded", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Other, -4).ChangeDisposition(NpcPersonalityAxis.Patience, -6), false);
            catalog.Memory(new MemoryDefinition("boundary_defiance_witnessed", "I saw a warning defied", 2, 2,
                new[] { new MemoryTrigger("boundary_defied", (a, e) => a != e.Subject && a != e.Other) },
                new MemoryOutcome(null, "vigilant", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, 0).ChangeDisposition(NpcPersonalityAxis.Patience, -3), false);
            catalog.Memory(new MemoryDefinition("boundary_violence", "Violence followed a warning", 2, 2,
                new[] { new MemoryTrigger("boundary_escalated", (a, e) => a == e.Other || a != e.Subject && a != e.Other) },
                new MemoryOutcome(null, "vigilant", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -10).ChangeDisposition(NpcPersonalityAxis.Patience, -10), false);
            catalog.Memory(new MemoryDefinition("boundary_lashed_out", "I struck someone who warned me", 2, 2,
                new[] { new MemoryTrigger("boundary_escalated", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "hotheaded", null), new MemoryOutcome(null, null, Skills.IDs.MARTIAL_ARTS))
                .Relate(MemoryRelationRole.Other, -5).ChangeDisposition(NpcPersonalityAxis.Patience, -8), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("base_theft", c => {
                if (c.Fact.NamesSubject) NpcReputation.Reputation(c, false, false, true);
            });
            catalog.OnReport("stolen_goods_passed", c => NpcReputation.StolenGoods(c.Listener, c.Fact, c.Improvement));
            catalog.OnReport("stolen_goods_found", c => NpcReputation.StolenGoods(c.Listener, c.Fact, c.Improvement));
            catalog.OnReport("claimed_permission", NpcReputation.PermissionClaim);
            catalog.OnReport("false_testimony_exposed", c => NpcReputation.Reputation(c, false, false, true));
            catalog.AfterEvent("base_theft", (game, e) => {
                if (e.Subject == null || e.Subject.Personality == null || !e.Subject.Personality.HasTrait("deceptive")) return;
                NpcFact theft = e.Subject.Personality.Knowledge.Facts.Find(f => f.EventId == e.Id && f.Kind == "base_theft");
                if (theft == null) return;
                e.Subject.Personality.Knowledge.Learn(new NpcFact { EventId = e.Id, Kind = "claimed_permission",
                    SubjectId = theft.SubjectId, OtherId = theft.OtherId, SubjectName = theft.SubjectName, OtherName = theft.OtherName,
                    SubjectReportName = theft.SubjectReportName, OtherReportName = theft.OtherReportName,
                    SubjectFactionId = theft.SubjectFactionId, OtherFactionId = theft.OtherFactionId,
                    EventTurn = e.Turn, LearnedTurn = e.Turn, Source = NpcKnowledgeSource.Participant,
                    SourceId = e.Subject.PersonalityIdentity, Confidence = 90, Place = theft.Place,
                    StoryId = theft.StoryId });
            });
            catalog.OnReport("boundary_defied", c => NpcReputation.Reputation(c, false, false, true));
            catalog.Event(new NpcEventDefinition("confronted", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " warned " + (e.Other ?? "someone") + " about known misconduct.", null) { StoryStage = (g, s, e) => "completed" });
            catalog.Event(new NpcEventDefinition("boundary_accepted", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " accepted " + (e.Other ?? "someone") + "'s boundary.",
                f => (f.ReportSubject ?? "Someone") + " agreed to respect " + (f.ReportOther ?? "someone") + "'s warning")
                { ReportConclusion = true, ImmediateSanity = NpcImmediateEmotion.Patience });
            catalog.Event(new NpcEventDefinition("boundary_defied", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " rejected " + (e.Other ?? "someone") + "'s boundary.", f => f.ReportSubject + " rejected " + (f.ReportOther == null ? "a" : f.ReportOther + "'s") + " warning")
                { SelfReportTone = NpcSelfReportTone.Harmful, ImmediateSanity = NpcImmediateEmotion.Patience });
            catalog.Event(new NpcEventDefinition("boundary_escalated", NpcRecordCategory.Combat, true,
                e => (e.Subject ?? "Someone") + " struck at " + (e.Other ?? "someone") + " after a warning.",
                f => (f.ReportSubject ?? "Someone") + " struck at " + (f.ReportOther ?? "someone") + " after a warning")
                { SelfReportTone = NpcSelfReportTone.Harmful, ReportPriority = 8 });
            catalog.Event(new NpcEventDefinition("base_theft", NpcRecordCategory.None, true,
                e => (e.Subject ?? "Someone") + " stole from " + (e.Other ?? "someone") + "'s base.",
                f => f.NamesSubject ? f.ReportSubject + " stole from " + (f.ReportOther ?? "someone") + "'s base" :
                    (f.ReportOther ?? "Someone") + "'s base was robbed")
                { SelfReportTone = NpcSelfReportTone.Harmful, ImmediateSanity = NpcImmediateEmotion.Scarcity });
            catalog.Event(new NpcEventDefinition("stolen_goods_passed", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " passed on stolen supplies from " + (e.Other ?? "a shelter") + ".",
                f => (f.ReportSubject ?? "Someone") + " passed on " + (f.Resource ?? "supplies") +
                    " stolen from " + (f.ReportOther ?? "a survivor") + "'s shelter")
                { ReportActorRole = NpcReportActorRole.Subject,
                  CanObserve = (a, e) => KnowsStolenVictim(a, e),
                  CanWitness = (a, e) => KnowsStolenVictim(a, e),
                  RadioSanity = NpcImmediateEmotion.RadioStolenGoods });
            catalog.Event(new NpcEventDefinition("stolen_goods_found", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " found stolen supplies.",
                f => (f.ReportSubject ?? "Someone") + " found " + (f.Resource ?? "supplies") +
                    " stolen from " + (f.ReportOther ?? "a survivor") + "'s shelter")
                { ReportActorRole = NpcReportActorRole.None, ReportConclusion = true,
                  CanObserve = (a, e) => KnowsStolenVictim(a, e),
                  CanWitness = (a, e) => KnowsStolenVictim(a, e),
                  RadioSanity = NpcImmediateEmotion.RadioStolenGoods });
            catalog.Event(new NpcEventDefinition("base_raid", NpcRecordCategory.Combat | NpcRecordCategory.World, true,
                e => (e.Other ?? "A group member") + " was killed at " + (e.Subject ?? "someone") + "'s base.",
                f => (f.ReportSubject ?? "someone") + "'s base was raided; " +
                    (f.ReportOther ?? "a member") + " was killed")
                { ReportActorRole = NpcReportActorRole.None });
            catalog.Event(new NpcEventDefinition("claimed_permission", NpcRecordCategory.None, false,
                e => (e.Subject ?? "Someone") + " claimed permission to take supplies.",
                f => f.ReportSubject + " claimed permission from " + (f.ReportOther ?? "the owner") + " to take supplies")
                { SelfReportTone = NpcSelfReportTone.Neutral });
            catalog.Event(new NpcEventDefinition("false_testimony_exposed", NpcRecordCategory.Encounters, false,
                e => (e.Other ?? "Someone") + " rejected " + (e.Subject ?? "someone") + "'s permission claim after witnessing the theft.",
                f => f.ReportSubject + " gave an account contradicted by an eyewitness", isPrivate: true)
                { ReportDisputesKinds = new[] { "base_theft", "claimed_permission" }, ReportPriority = 8 });
            catalog.Memory(new MemoryDefinition("false_testimony_exposed", "Caught a contradictory permission claim", 2, 2,
                new[] { new MemoryTrigger("false_testimony_exposed", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, -8).ChangeDisposition(NpcPersonalityAxis.MessageTrust, -15), false);
            catalog.Event(new NpcEventDefinition("restitution_given", NpcRecordCategory.Help, false, e => (e.Subject ?? "Someone") + " replaced supplies lost by " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("restitution_requested", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to compensate lost supplies.", null) { AudibleReport = true, PlayerReply = new NpcPlayerReply("replacement supplies", "food_promised", "restitution_refused", "Yes, I'll replace your supplies.", "No, I won't replace them.") });
            catalog.Event(new NpcEventDefinition("restitution_refused", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " refused " + (e.Other ?? "someone") + "'s demand for compensation.", null) { SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.On("base_theft", NpcObservationPhase.Knowledge, OnKnowledge);
            catalog.On("base_theft", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("boundary_accepted", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("boundary_defied", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("confronted", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_given", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_refused", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("restitution_requested", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("shared_food", NpcObservationPhase.Relationships, OnRelationships);
        }
        static bool KnowsStolenVictim(Actor observer, SignificantEvent source)
        {
            if (observer.Personality == null || source.ClaimantId == Guid.Empty) return false;
            return observer.PersonalityIdentity == source.ClaimantId ||
                observer.SocialGroup != null && observer.SocialGroup.Identity == source.ClaimantGroupId ||
                observer.Personality.Person(source.ClaimantId) != null ||
                observer.Personality.Knowledge.Person(source.ClaimantId) != null;
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
                RelationshipRecord opinion = owner.Personality.Opinion(subject.Id, subject.Name);
                if (source.Kind == "boundary_accepted")
                {
                    bool answersCurrentWarning = false;
                    foreach (ObservedEvent warning in owner.Personality.Events)
                        if (warning.Kind == "confronted" && warning.EventId == source.CauseId &&
                            warning.SubjectId == owner.PersonalityIdentity && warning.OtherId == subject.Id &&
                            warning.CauseId == subject.ViolationCause)
                        { answersCurrentWarning = true; break; }
                    int acceptance = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion) +
                        PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law) / 4 +
                        PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience, observation.Game.NpcContent) / 2 +
                        PersonalitySystem.Disposition(owner, NpcPersonalityAxis.MessageTrust, observation.Game.NpcContent) / 2 -
                        opinion.Grievance / 2;
                    if (answersCurrentWarning && acceptance >= 0)
                    { subject.AcknowledgedViolation = subject.ViolationCause; subject.Violation = 0; knowledge.Revision++; }
                    opinion.AdjustSocial(trust: 5, grievance: -5);
                }
                else
                {
                    subject.Violation = 100;
                    subject.ViolationCause = source.Id; subject.ViolationTurn = source.Turn; subject.ViolationConfidence = 100;
                    opinion.AdjustSocial(trust: -10, grievance: 10);
                    knowledge.Revision++;
                }
            }
            if (source.Kind == "confronted" && source.Other == owner && NpcIntentSystem.Enabled(owner))
            {
                int patience = PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience, observation.Game.NpcContent);
                if (!NpcReplies.Setback(observation, 25, "Don't tell me what to do!",
                    "boundary_escalated", "boundary_defied"))
                    NpcReplies.Reply(owner, source.Subject, source, PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law) +
                        PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion) + patience / 2 >= 0 ?
                        "boundary_accepted" : "boundary_defied", "I understand. I'll respect that.", "You don't decide what I do.");
            }
            if (source.Kind == "restitution_requested" && source.Other == owner && subject != null && NpcIntentSystem.Enabled(owner))
            {
                bool accept = PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law) + PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion) >= 10;
                if (accept && subject.LossCause > 0) subject.LossUnits = Math.Max(subject.LossUnits, source.Units);
                NpcReplies.Reply(owner, source.Subject, source, accept ? "boundary_accepted" : "restitution_refused", "I'll try to replace what was lost.", "I won't replace your supplies.");
            }
        }
    }
}
