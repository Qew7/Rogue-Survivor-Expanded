using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcGoalOffers
    {
        readonly NpcGoalContext context;
        public readonly List<NpcGoalCandidate> Candidates = new List<NpcGoalCandidate>();
        public NpcGoalOffers(NpcGoalContext context) { this.context = context; }
        public void Add(NpcKnownPerson target, NpcGoalValue value, NpcIntentDefinition capability,
            int current, int desired, int deficit, int confidence, long cause = 0, string story = null,
            string resource = null, long obligation = 0, int model = -1, Location objectPlace = default(Location), Guid itemId = default(Guid), bool self = false)
        {
            NpcValueDefinition definition = context.Catalog.Value(value);
            if (definition == null) throw new ArgumentException("Unknown goal value: " + value);
            if (capability == null) throw new ArgumentException("Missing capability for goal value: " + value);
            Add(target, definition.Id, capability.Id, current, desired, deficit, confidence,
                cause, story, resource, obligation, model, objectPlace, itemId, self);
        }
        public void Add(NpcKnownPerson target, string valueId, string capabilityId,
            int current, int desired, int deficit, int confidence, long cause = 0, string story = null,
            string resource = null, long obligation = 0, int model = -1, Location objectPlace = default(Location), Guid itemId = default(Guid), bool self = false)
        {
            if (Candidates.Count >= 192 || target == null || target.Dead || target.Place.Map == null) return;
            NpcValueDefinition definition = context.Catalog.Value(valueId); NpcIntentDefinition capability = context.Catalog.Capability(capabilityId);
            if (definition == null || capability == null) throw new ArgumentException("Unknown goal value or capability.");
            Guid subject = self ? context.Self.Id : target.Id;
            NpcGeneratedGoal state = NpcValues.Evaluate(context.Owner, definition, subject, current, desired, deficit, confidence, 0, context.Catalog.Personalities, context.Catalog);
            state.Resource = resource; state.ObligationId = obligation; state.ModelId = model; state.ObjectPlace = objectPlace; state.ItemId = itemId;
            state.IdentitySuffix = definition.IdentitySuffix == null ? "" : definition.IdentitySuffix(state);
            state.ResultState = capability.GetResult(context.Catalog, state);
            state.Causes = cause > 0 && target.SocialCause > 0 && target.SocialCause != cause ? new[] { cause, target.SocialCause } :
                cause > 0 ? new[] { cause } : target.SocialCause > 0 ? new[] { target.SocialCause } : new long[0];
            Candidates.Add(new NpcGoalCandidate { Target = target, Capability = capability, State = state, Cause = cause, Story = story });
        }
    }
}
