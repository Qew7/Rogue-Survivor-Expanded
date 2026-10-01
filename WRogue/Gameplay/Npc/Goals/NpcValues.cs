using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Values describe desirable states. They never dispatch on event kinds.
    static class NpcValues
    {
        public static int Importance(NpcContentCatalog catalog, Actor owner, NpcGoalValue value, Guid subject)
        {
            NpcValueDefinition definition = catalog.Value(value);
            return definition == null ? 0 : definition.Importance(new NpcMotivation(catalog, owner, subject));
        }
        public static int KnownAttitude(Actor owner, Guid subject, PersonalityRegistry registry = null,
            string excludeTrait = null)
        {
            NpcKnownPerson known = owner.Personality.Knowledge.Person(subject);
            return NpcRelationshipValue.Calculate(owner, subject, known == null ? Guid.Empty : known.GroupId,
                known == null ? -1 : known.FactionId, true, registry, excludeTrait);
        }
        public static string TraitInfluence(Actor owner, NpcGeneratedGoal goal, NpcContentCatalog catalog)
        {
            if (goal == null || owner.Personality == null) return null;
            NpcValueDefinition value = catalog.Value(goal);
            if (value == null) return null;
            string strongest = null;
            int largest = 0;
            foreach (TraitInstance trait in owner.Personality.Traits)
            {
                TraitDefinition definition = catalog.Personalities.Trait(trait.Id);
                if (definition == null) continue;
                int without = Math.Max(0, Math.Min(200, value.Importance(
                    new NpcMotivation(catalog, owner, goal.SubjectId, trait.Id))));
                int increase = goal.Importance - without;
                if (increase > largest) { largest = increase; strongest = definition.Name; }
            }
            return strongest == null ? null : "because trait " + strongest + " raised this goal's importance by " + largest;
        }
        public static NpcGeneratedGoal Evaluate(NpcContentCatalog catalog, Actor owner,
            NpcValueDefinition definition, Guid subject, int current, int desired, int deficit,
            int confidence, ulong result)
        {
            int importance = Math.Max(0, Math.Min(200, definition.Importance(new NpcMotivation(catalog, owner, subject))));
            deficit = Math.Max(0, Math.Min(100, deficit)); confidence = Math.Max(0, Math.Min(100, confidence));
            return new NpcGeneratedGoal { Value = definition.LegacyValue ?? default(NpcGoalValue),
                DefinitionId = definition.LegacyValue.HasValue ? null : definition.Id,
                DefinitionDescription = definition.LegacyValue.HasValue ? null : definition.Description, SubjectId = subject, Current = current, Desired = desired,
                Deficit = deficit, Importance = importance, Confidence = confidence,
                Utility = deficit * importance * confidence / 10000, Result = result,
                EvaluatedTurn = owner.Location.Map.LocalTime.TurnCounter };
        }
    }
}
