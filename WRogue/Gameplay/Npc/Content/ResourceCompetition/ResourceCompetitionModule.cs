using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class ResourceCompetitionModule : INpcContentModule, INpcActionGuard
    {
        public string Id { get { return "resource-competition"; } }
        public void Register(NpcCatalogBuilder catalog) {
            catalog.AfterEvent("resource_yielded", (g, e) => { if (e.Subject != null) Session.Get.NpcDirector.ReleaseOwned(e.ResourcePlace, e.Subject.PersonalityIdentity); UpdateRule(g, e, e.Subject, -15); });
            catalog.AfterEvent("base_theft", (g, e) => UpdateRule(g, e, e.Other, 20));
            catalog.AfterEvent("traded", (g, e) => UpdateTrade(g, e));
            catalog.AfterEvent("bartered_food", (g, e) => UpdateTrade(g, e));
            catalog.AfterEvent("bartered_medicine", (g, e) => UpdateTrade(g, e));
            catalog.ActionGuard(this); RegisterContent(catalog);
        }
        static void UpdateTrade(RogueGame game, SignificantEvent source)
        {
            UpdateRule(game, source, source.Subject, -10);
            if (source.Other != source.Subject) UpdateRule(game, source, source.Other, -10);
        }
        static void UpdateRule(RogueGame game, SignificantEvent source, Actor leader, int change)
        {
            if (leader == null || leader.Personality == null || leader.SocialGroup == null ||
                leader.SocialGroup.LeaderId != leader.PersonalityIdentity ||
                !leader.Personality.Events.Any(e => e.EventId == source.Id)) return;
            SocialGroup group = leader.SocialGroup;
            if (group.LastSupplyRuleEvent == source.Id) return;
            group.LastSupplyRuleEvent = source.Id;
            int before = group.SupplyRule;
            group.SupplyRule = Math.Max(0, Math.Min(40, before + change));
            if ((before < 30 && group.SupplyRule >= 30) || (before >= 30 && group.SupplyRule < 30))
                PersonalitySystem.Report(game, new SignificantEvent(group.SupplyRule >= 30 ? "group_supply_rule_strict" : "group_supply_rule_open",
                    leader, null, leader.Location.Map, leader.Location.Position, source.Turn, causeId: source.Id));
        }
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
            NpcMemoryContent.Received(catalog, "permission_respected", "Someone used permission to take supplies", "permission_used", 5, null);
            catalog.Memory(new MemoryDefinition("group_supply_rule", "The group changed its supply rule", 2, 5,
                new[] { new MemoryTrigger("group_supply_rule_strict", (a, e) => e.Subject != null && a != e.Subject &&
                    a.SocialGroup != null && a.SocialGroup == e.Subject.SocialGroup),
                    new MemoryTrigger("group_supply_rule_open", (a, e) => e.Subject != null && a != e.Subject &&
                    a.SocialGroup != null && a.SocialGroup == e.Subject.SocialGroup) },
                new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP)).Relate(MemoryRelationRole.Subject, 0, MemoryRelationRole.Subject), false);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.OnReport("contested_taken", c => NpcReputation.Reputation(c, false, false, true));
            catalog.Event(new NpcEventDefinition("resource_contested", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to yield disputed supplies.", null));
            catalog.Event(new NpcEventDefinition("resource_yielded", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " yielded disputed supplies to " + (e.Other ?? "someone") + ".", f => f.ReportSubject + " allowed " + (f.ReportOther ?? "someone") + " to take disputed supplies") { SelfReportTone = NpcSelfReportTone.Helpful });
            catalog.Event(new NpcEventDefinition("permission_used", NpcRecordCategory.Help, true, e => (e.Subject ?? "Someone") + " took supplies with " + (e.Other ?? "someone") + "'s permission.", f => f.ReportSubject + " took supplies with " + (f.ReportOther ?? "someone") + "'s permission") { SelfReportTone = NpcSelfReportTone.Helpful });
            catalog.Event(new NpcEventDefinition("group_supply_rule_strict", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " asked the group to seek permission before taking supplies.",
                f => f.ReportSubject + " asked the group to seek permission before taking supplies"));
            catalog.Event(new NpcEventDefinition("group_supply_rule_open", NpcRecordCategory.Encounters, true,
                e => (e.Subject ?? "Someone") + " relaxed the group's supply rule.",
                f => f.ReportSubject + " relaxed the group's supply rule"));
            catalog.OnReport("group_supply_rule_strict", c => HearRule(c, 40));
            catalog.OnReport("group_supply_rule_open", c => HearRule(c, 0));
            catalog.Event(new NpcEventDefinition("resource_refused", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " refused to yield supplies to " + (e.Other ?? "someone") + ".", null));
            catalog.Event(new NpcEventDefinition("contested_taken", NpcRecordCategory.None, true, e => (e.Subject ?? "Someone") + " took supplies despite " + (e.Other ?? "someone") + "'s refusal.", f => f.ReportSubject + " took disputed supplies" + (f.ReportOther == null ? "" : " despite " + f.ReportOther + "'s refusal")) { SelfReportTone = NpcSelfReportTone.Harmful });
            catalog.On("contested_taken", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_contested", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_refused", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("resource_yielded", NpcObservationPhase.Relationships, OnRelationships);
            catalog.On("permission_used", NpcObservationPhase.Relationships, o => {
                if (o.Owner == o.Source.Other && o.Source.Subject != null)
                    o.Owner.Personality.Opinion(o.Source.Subject.PersonalityIdentity, o.Source.Subject.UnmodifiedName).AdjustSocial(trust: 5);
            });
            Action<NpcObservation> ruleReaction = o => {
                if (o.Source.Subject == null || o.Owner.SocialGroup == null || o.Owner.SocialGroup != o.Source.Subject.SocialGroup) return;
                o.Owner.Personality.LearnSupplyRule(o.Owner.SocialGroup.Identity,
                    o.Source.Kind == "group_supply_rule_strict" ? 40 : 0, o.Source.Id);
                if (o.Owner == o.Source.Subject) return;
                int approval = PersonalitySystem.Bias(o.Owner, DecisionKind.Law) + PersonalitySystem.Bias(o.Owner, DecisionKind.Supplies) - PersonalitySystem.Bias(o.Owner, DecisionKind.Group) / 2;
                if (o.Source.Kind == "group_supply_rule_open") approval = -approval;
                o.Owner.Personality.Opinion(o.Source.Subject.PersonalityIdentity, o.Source.Subject.UnmodifiedName).AdjustSocial(
                    trust: approval >= 0 ? 5 : -8, grievance: approval >= 0 ? 0 : 5);
            };
            catalog.On("group_supply_rule_strict", NpcObservationPhase.Relationships, ruleReaction);
            catalog.On("group_supply_rule_open", NpcObservationPhase.Relationships, ruleReaction);
        }
        static void HearRule(NpcReportContext report, int rule)
        {
            Actor listener = report.Listener;
            NpcFact fact = report.Fact;
            if (report.Improvement <= 0 || fact.Confidence < 40 || !fact.NamesSubject ||
                listener.SocialGroup == null || listener.SocialGroup.LeaderId != fact.SubjectId) return;
            listener.Personality.LearnSupplyRule(listener.SocialGroup.Identity, rule, fact.EventId);
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
