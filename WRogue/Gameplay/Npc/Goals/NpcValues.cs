using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Values describe desirable states. They never dispatch on event kinds.
    static class NpcValues
    {
        public static int Importance(Actor owner, NpcGoalValue value, Guid subject)
        {
            NpcValueDefinition definition = NpcContentCatalog.Default.Value(value);
            return definition == null ? 0 : definition.Importance(new NpcMotivation(owner, subject));
        }
        public static int KnownAttitude(Actor owner, Guid subject, PersonalityRegistry registry = null)
        {
            NpcKnownPerson known = owner.Personality.Knowledge.Person(subject);
            return NpcRelationshipValue.Calculate(owner, subject, known == null ? Guid.Empty : known.GroupId,
                known == null ? -1 : known.FactionId, true, registry);
        }
        public static NpcGeneratedGoal Evaluate(Actor owner, NpcGoalValue value, Guid subject,
            int current, int desired, int deficit, int confidence, ulong result)
        {
            return Evaluate(owner, NpcContentCatalog.Default.Value(value), subject, current, desired, deficit, confidence, result);
        }
        public static NpcGeneratedGoal Evaluate(Actor owner, NpcValueDefinition definition, Guid subject,
            int current, int desired, int deficit, int confidence, ulong result, PersonalityRegistry registry = null)
        {
            int importance = Math.Max(0, Math.Min(200, definition.Importance(new NpcMotivation(owner, subject, registry))));
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
