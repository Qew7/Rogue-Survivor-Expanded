using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcCatalogBuilder
    {
        readonly PersonalityRegistry personalities;
        internal PersonalityRegistry Personalities { get { return personalities; } }
        internal readonly Dictionary<string, NpcValueDefinition> Values = new Dictionary<string, NpcValueDefinition>();
        internal readonly Dictionary<string, NpcIntentDefinition> Capabilities = new Dictionary<string, NpcIntentDefinition>();
        internal readonly Dictionary<string, NpcOperatorDefinition> Operators = new Dictionary<string, NpcOperatorDefinition>();
        internal readonly Dictionary<string, NpcOperatorSource> OperatorSources = new Dictionary<string, NpcOperatorSource>();
        internal readonly List<INpcGoalSource> Sources = new List<INpcGoalSource>();
        internal readonly List<Action<NpcPlanDomain>> Seeds = new List<Action<NpcPlanDomain>>();
        internal readonly List<string> Facts = new List<string>();
        internal readonly Dictionary<string, NpcEventDefinition> Events = new Dictionary<string, NpcEventDefinition>();
        internal readonly List<INpcActionGuard> Guards = new List<INpcActionGuard>();
        sealed class Subscription { public string Event; public NpcObservationPhase Phase; public Action<NpcObservation> Observer; }
        readonly List<Subscription> subscriptions = new List<Subscription>();
        internal readonly Dictionary<NpcPerceptionKind, List<Action<NpcPerceptionContext>>> Perceptions = new Dictionary<NpcPerceptionKind, List<Action<NpcPerceptionContext>>>();
        sealed class ReportSubscription { public string Kind; public Action<NpcReportContext> Observer; }
        readonly List<ReportSubscription> reportSubscriptions = new List<ReportSubscription>();
        internal readonly Dictionary<NpcClockPhase, List<Action<NpcClockContext>>> Clocks = new Dictionary<NpcClockPhase, List<Action<NpcClockContext>>>();
        sealed class CompletionSubscription { public string Kind; public Action<djack.RogueSurvivor.Engine.RogueGame, SignificantEvent> Observer; }
        readonly List<CompletionSubscription> completions = new List<CompletionSubscription>();
        internal readonly Dictionary<int, NpcFactionPolicy> Factions = new Dictionary<int, NpcFactionPolicy>();
        internal readonly Dictionary<string, NpcResourceDefinition> Resources = new Dictionary<string, NpcResourceDefinition>();
        internal readonly Dictionary<string, int> Revisions = new Dictionary<string, int>();
        internal readonly Dictionary<string, NpcCollectiveDefinition> Collectives = new Dictionary<string, NpcCollectiveDefinition>();
        internal readonly Dictionary<string, NpcInterestDefinition> Interests = new Dictionary<string, NpcInterestDefinition>();
        bool built;
        public NpcCatalogBuilder(PersonalityRegistry personalities) { this.personalities = personalities; }
        void Writable() { if (built) throw new InvalidOperationException("NPC catalog is already built."); }
        static void Put<T>(Dictionary<string, T> entries, string id, T value)
        {
            if (String.IsNullOrWhiteSpace(id) || value == null || entries.ContainsKey(id))
                throw new ArgumentException("Duplicate or empty NPC content ID: " + id);
            entries.Add(id, value);
        }
        public void Revision(string id, int version)
        { Writable(); if (String.IsNullOrWhiteSpace(id) || version < 1 || Revisions.ContainsKey(id)) throw new ArgumentException("Invalid or duplicate content revision."); Revisions.Add(id, version); }
        public void Resource(NpcResourceDefinition definition)
        { Writable(); if (definition.OwnNeed == null) throw new ArgumentException("Missing resource need assessment."); Put(Resources, definition.Id, definition); }
        public void Value(NpcValueDefinition value) { Writable(); Put(Values, value.Id, value); }
        public void Capability(NpcIntentDefinition capability) { Writable(); Put(Capabilities, capability.Id, capability); }
        public void Operator(NpcOperatorDefinition action) { Writable(); Put(Operators, action.Id, action); }
        public void Interest(NpcInterestDefinition definition)
        { Writable(); if (definition.Observe == null || definition.Evaluate == null) throw new ArgumentException("Incomplete lasting interest."); Put(Interests, definition.Id, definition); }
        public void Collective(NpcCollectiveDefinition definition)
        { Writable(); if (definition.Propose == null || definition.Accept == null || definition.Message == null || definition.CanCommunicate == null) throw new ArgumentException("Incomplete collective task."); Put(Collectives, definition.Id, definition); }
        public void OperatorSource(NpcOperatorSource source)
        { Writable(); if (source.Produces == null || source.Bind == null) throw new ArgumentException("Missing operator-source contract."); Put(OperatorSources, source.Id, source); }
        public void FactionPolicy(int id, NpcFactionPolicy policy)
        { Writable(); if (id < 0 || policy == null || Factions.ContainsKey(id)) throw new ArgumentException("Invalid or duplicate faction policy."); Factions.Add(id, policy); }
        public void Clock(NpcClockPhase phase, Action<NpcClockContext> observer)
        {
            Writable(); if (observer == null) throw new ArgumentNullException("observer"); List<Action<NpcClockContext>> list;
            if (!Clocks.TryGetValue(phase, out list)) Clocks.Add(phase, list = new List<Action<NpcClockContext>>()); list.Add(observer);
        }
        public void Perception(NpcPerceptionKind kind, Action<NpcPerceptionContext> observer)
        {
            Writable(); if (observer == null) throw new ArgumentNullException("observer");
            List<Action<NpcPerceptionContext>> list;
            if (!Perceptions.TryGetValue(kind, out list)) Perceptions.Add(kind, list = new List<Action<NpcPerceptionContext>>());
            list.Add(observer);
        }
        public void AfterEvent(string kind, Action<djack.RogueSurvivor.Engine.RogueGame, SignificantEvent> observer)
        { Writable(); completions.Add(new CompletionSubscription { Kind = kind, Observer = observer }); }
        public void OnReport(string kind, Action<NpcReportContext> observer)
        { Writable(); reportSubscriptions.Add(new ReportSubscription { Kind = kind, Observer = observer }); }
        public void On(string kind, NpcObservationPhase phase, Action<NpcObservation> observer)
        { Writable(); subscriptions.Add(new Subscription { Event = kind, Phase = phase, Observer = observer }); }
        public void Event(NpcEventDefinition definition) { Writable(); Put(Events, definition.Id, definition); }
        public void GoalSource(INpcGoalSource source) { Writable(); if (source == null) throw new ArgumentNullException("source"); Sources.Add(source); }
        public void ActionGuard(INpcActionGuard guard) { Writable(); if (guard == null) throw new ArgumentNullException("guard"); Guards.Add(guard); }
        public void PlanSeed(Action<NpcPlanDomain> seed) { Writable(); if (seed == null) throw new ArgumentNullException("seed"); Seeds.Add(seed); }
        public void Fact(string id)
        { Writable(); if (String.IsNullOrWhiteSpace(id) || Facts.Contains(id)) throw new ArgumentException("Duplicate or empty fact ID."); Facts.Add(id); }
        public void Trait(TraitDefinition definition) { Writable(); personalities.Register(definition); }
        public void Memory(MemoryDefinition definition, bool starting = false) { Writable(); personalities.Register(definition, starting); }
        public NpcContentCatalog Build()
        {
            Writable();
            foreach (TraitDefinition trait in personalities.AllTraits)
                if (trait.RequiresTrait != null && personalities.Trait(trait.RequiresTrait) == null) throw new ArgumentException("Unknown trait prerequisite: " + trait.RequiresTrait);
            foreach (NpcValueDefinition value in Values.Values)
            {
                if (value.Importance == null) throw new ArgumentException("Missing motivation: " + value.Id);
                foreach (string id in value.EquivalentCapabilities) if (!Capabilities.ContainsKey(id)) throw new ArgumentException("Unknown equivalent capability: " + id);
                foreach (string id in value.CooldownAliases) if (!Capabilities.ContainsKey(id)) throw new ArgumentException("Unknown cooldown alias: " + id);
            }
            foreach (NpcIntentDefinition capability in Capabilities.Values)
                if (capability.Selectable && capability.Result == null && capability.ResultFacts == null)
                    throw new ArgumentException("Missing desired result: " + capability.Id);
            foreach (NpcOperatorDefinition op in Operators.Values) if (op.Execute == null) throw new ArgumentException("Missing executor: " + op.Id);
            foreach (MemoryDefinition memory in personalities.AllMemories)
            {
                foreach (MemoryOutcome outcome in memory.Outcomes)
                    if (outcome.TraitId != null && personalities.Trait(outcome.TraitId) == null) throw new ArgumentException("Unknown memory outcome: " + outcome.TraitId);
                foreach (MemoryTrigger trigger in memory.Triggers) if (!Events.ContainsKey(trigger.EventKind)) throw new ArgumentException("Unknown memory event: " + trigger.EventKind);
                foreach (string kind in memory.EvidenceKinds) if (!Events.ContainsKey(kind)) throw new ArgumentException("Unknown evidence event: " + kind);
            }
            foreach (Subscription subscription in subscriptions)
            {
                NpcEventDefinition definition;
                if (!Events.TryGetValue(subscription.Event, out definition)) throw new ArgumentException("Unknown subscribed event: " + subscription.Event);
                definition.Subscribe(subscription.Phase, subscription.Observer);
            }
            foreach (ReportSubscription subscription in reportSubscriptions)
            {
                NpcEventDefinition definition;
                if (!Events.TryGetValue(subscription.Kind, out definition)) throw new ArgumentException("Unknown report event: " + subscription.Kind);
                definition.SubscribeReport(subscription.Observer);
            }
            foreach (CompletionSubscription subscription in completions)
            {
                NpcEventDefinition definition;
                if (!Events.TryGetValue(subscription.Kind, out definition)) throw new ArgumentException("Unknown completion event: " + subscription.Kind);
                definition.SubscribeCompletion(subscription.Observer);
            }
            foreach (NpcCollectiveDefinition collective in Collectives.Values)
                if (!Events.ContainsKey(collective.EventId)) throw new ArgumentException("Unknown collective communication event: " + collective.EventId);
            foreach (NpcEventDefinition definition in Events.Values) definition.Freeze();
            NpcContentCatalog catalog = new NpcContentCatalog(this);
            built = true; return catalog;
        }
        public static NpcContentCatalog Compose(PersonalityRegistry personalities, params INpcContentModule[] modules)
        {
            var builder = new NpcCatalogBuilder(personalities); var ids = new HashSet<string>();
            builder.Revision("runtime.schema", 1);
            foreach (INpcContentModule module in modules)
            {
                if (module == null || String.IsNullOrWhiteSpace(module.Id) || !ids.Add(module.Id)) throw new ArgumentException("Duplicate or empty NPC module ID.");
                builder.Revision("module:" + module.Id, 1); module.Register(builder);
            }
            return builder.Build();
        }
    }
}
