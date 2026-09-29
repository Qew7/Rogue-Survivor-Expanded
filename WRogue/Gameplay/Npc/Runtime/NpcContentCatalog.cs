using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcContentCatalog
    {
        public static NpcContentCatalog Default { get { return PersonalitySystem.Registry.Content; } }
        readonly Dictionary<string, NpcResourceDefinition> resources;
        readonly Dictionary<string, NpcValueDefinition> values;
        readonly Dictionary<NpcGoalValue, NpcValueDefinition> legacyValues = new Dictionary<NpcGoalValue, NpcValueDefinition>();
        readonly Dictionary<string, NpcIntentDefinition> capabilities;
        readonly Dictionary<string, NpcOperatorDefinition> operators;
        readonly Dictionary<NpcPlanAction, NpcOperatorDefinition> legacyOperators = new Dictionary<NpcPlanAction, NpcOperatorDefinition>();
        readonly Dictionary<NpcIntentMethod, NpcIntentDefinition> legacyMethods = new Dictionary<NpcIntentMethod, NpcIntentDefinition>();
        readonly Dictionary<string, NpcEventDefinition> events;
        public readonly IList<INpcGoalSource> GoalSources;
        public readonly IList<Action<NpcPlanDomain>> PlanSeeds;
        public readonly PersonalityRegistry Personalities;
        public readonly IList<INpcActionGuard> ActionGuards;
        public readonly NpcFactLayout Facts;
        readonly Dictionary<NpcPerceptionKind, IList<Action<NpcPerceptionContext>>> perceptions = new Dictionary<NpcPerceptionKind, IList<Action<NpcPerceptionContext>>>();
        readonly Dictionary<NpcClockPhase, IList<Action<NpcClockContext>>> clocks = new Dictionary<NpcClockPhase, IList<Action<NpcClockContext>>>();
        readonly Dictionary<int, NpcFactionPolicy> factions;
        static readonly NpcFactionPolicy neutral = new NpcFactionPolicy(0, 0);
        public readonly int Fingerprint;
        internal NpcContentCatalog(NpcCatalogBuilder builder)
        {
            resources = new Dictionary<string, NpcResourceDefinition>(builder.Resources);
            factions = new Dictionary<int, NpcFactionPolicy>(builder.Factions);
            Personalities = builder.Personalities;
            values = new Dictionary<string, NpcValueDefinition>(builder.Values);
            capabilities = new Dictionary<string, NpcIntentDefinition>(builder.Capabilities);
            operators = new Dictionary<string, NpcOperatorDefinition>(builder.Operators);
            events = new Dictionary<string, NpcEventDefinition>(builder.Events);
            ActionGuards = builder.Guards.AsReadOnly(); GoalSources = builder.Sources.AsReadOnly(); PlanSeeds = builder.Seeds.AsReadOnly();
            foreach (NpcValueDefinition value in values.Values) if (value.LegacyValue.HasValue) legacyValues.Add(value.LegacyValue.Value, value);
            foreach (NpcOperatorDefinition op in operators.Values) if (op.LegacyAction.HasValue) legacyOperators.Add(op.LegacyAction.Value, op);
            foreach (NpcIntentDefinition capability in capabilities.Values)
                if (capability.LegacyMethod && !legacyMethods.ContainsKey(capability.Method)) legacyMethods.Add(capability.Method, capability);
            foreach (var pair in builder.Clocks) clocks.Add(pair.Key, pair.Value.AsReadOnly());
            foreach (var pair in builder.Perceptions) perceptions.Add(pair.Key, pair.Value.AsReadOnly());
            Facts = new NpcFactLayout(builder.Facts);
            // Stable across processes; string.GetHashCode is intentionally not used for saved plans.
            var keys = new List<string>();
            foreach (string id in values.Keys) keys.Add("value:" + id);
            foreach (string id in capabilities.Keys) keys.Add("capability:" + id);
            foreach (string id in operators.Keys) keys.Add("operator:" + id);
            foreach (string id in events.Keys) keys.Add("event:" + id);
            foreach (string id in resources.Keys) keys.Add("resource:" + id);
            foreach (TraitDefinition trait in Personalities.AllTraits) keys.Add("trait:" + trait.Id);
            foreach (MemoryDefinition memory in Personalities.AllMemories) keys.Add("memory:" + memory.Id);
            foreach (string id in builder.Facts) keys.Add("fact:" + id);
            foreach (var pair in builder.Revisions) keys.Add("revision:" + pair.Key + ":" + pair.Value);
            keys.Sort(StringComparer.Ordinal); int hash = 17;
            foreach (string key in keys) foreach (char c in key) hash = unchecked(hash * 31 + c);
            Fingerprint = hash;
        }
        public NpcResourceDefinition Resource(string id) { NpcResourceDefinition value; return id != null && resources.TryGetValue(id, out value) ? value : null; }
        public NpcValueDefinition Value(string id) { NpcValueDefinition value; return id != null && values.TryGetValue(id, out value) ? value : null; }
        public NpcValueDefinition Value(NpcGoalValue id) { NpcValueDefinition value; return legacyValues.TryGetValue(id, out value) ? value : null; }
        public NpcValueDefinition Value(NpcGeneratedGoal goal) { return goal.DefinitionId == null ? Value(goal.Value) : Value(goal.DefinitionId); }
        public NpcIntentDefinition Capability(string id) { NpcIntentDefinition value; return id != null && capabilities.TryGetValue(id, out value) ? value : null; }
        public NpcIntentDefinition Capability(NpcIntentMethod method) { NpcIntentDefinition value; return legacyMethods.TryGetValue(method, out value) ? value : null; }
        public NpcOperatorDefinition Operator(string id) { NpcOperatorDefinition value; return id != null && operators.TryGetValue(id, out value) ? value : null; }
        public NpcOperatorDefinition Operator(NpcPlanStep step)
        { NpcOperatorDefinition value; return step.OperatorId != null ? Operator(step.OperatorId) : legacyOperators.TryGetValue(step.Action, out value) ? value : null; }
        public NpcFactionPolicy FactionPolicy(int id) { NpcFactionPolicy policy; return factions.TryGetValue(id, out policy) ? policy : neutral; }
        public void Advance(NpcClockPhase phase, NpcClockContext context)
        { IList<Action<NpcClockContext>> handlers; if (clocks.TryGetValue(phase, out handlers)) foreach (var handler in handlers) handler(context); }
        public void Perceive(NpcPerceptionKind kind, NpcPerceptionContext context)
        { IList<Action<NpcPerceptionContext>> handlers; if (perceptions.TryGetValue(kind, out handlers)) foreach (var handler in handlers) handler(context); }
        public void Hear(NpcReportContext report)
        { NpcEventDefinition definition = Event(report.Fact.Kind); if (definition != null) definition.Hear(report); }
        public NpcEventDefinition Event(string id) { NpcEventDefinition value; return id != null && events.TryGetValue(id, out value) ? value : null; }
    }
}
