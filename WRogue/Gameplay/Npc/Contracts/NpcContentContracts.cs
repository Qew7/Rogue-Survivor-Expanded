using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Definitions are runtime content. Saves contain stable IDs and state, never delegates.
    interface INpcContentModule
    {
        string Id { get; }
        void Register(NpcCatalogBuilder catalog);
    }

    interface INpcGoalSource
    {
        void Evaluate(NpcGoalContext context, NpcGoalOffers offers);
    }

    interface INpcTraitInterest
    {
        void Prepare(Actor owner, TraitInstance trait);
        void Observe(Actor owner, TraitInstance trait, Inventory items, Location place, int risk);
    }

    sealed class NpcValueDefinition
    {
        public readonly string Id, Description;
        public readonly NpcGoalValue? LegacyValue;
        public readonly Func<NpcMotivation, int> Importance;
        public readonly bool CompleteWhenSatisfied;
        public readonly string[] CooldownAliases;
        public string[] EquivalentCapabilities = new string[0];
        public Func<NpcGeneratedGoal, string> IdentitySuffix;
        public Func<NpcActionContext, bool> AfterDelivery;
        public string ArrivalEvent;
        public NpcValueDefinition(string id, string description, NpcGoalValue? legacy,
            Func<NpcMotivation, int> importance, bool completeWhenSatisfied = false, params string[] cooldownAliases)
        {
            Id = id; Description = description; LegacyValue = legacy; Importance = importance;
            CompleteWhenSatisfied = completeWhenSatisfied; CooldownAliases = cooldownAliases;
        }
    }

    sealed class NpcOperatorDefinition
    {
        public readonly string Id;
        public readonly NpcPlanAction? LegacyAction;
        public readonly Func<NpcExecutionContext, ActorAction> Execute;
        public readonly string Resource;
        public readonly Action<NpcExecutionContext> Unavailable;
        public readonly Func<NpcPlanStep, string> ArchiveText;
        public NpcOperatorDefinition(string id, NpcPlanAction? legacy, Func<NpcExecutionContext, ActorAction> execute,
            string resource = null, Action<NpcExecutionContext> unavailable = null,
            Func<NpcPlanStep, string> archiveText = null)
        { Id = id; LegacyAction = legacy; Execute = execute; Resource = resource; Unavailable = unavailable; ArchiveText = archiveText; }
    }

    sealed class NpcIntentOutcome
    {
        public readonly NpcIntentStatus Status;
        public readonly string Reason;
        public NpcIntentOutcome(NpcIntentStatus status, string reason) { Status = status; Reason = reason; }
    }
}
