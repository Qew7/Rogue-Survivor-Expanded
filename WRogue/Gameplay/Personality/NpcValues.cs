using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Values describe desirable states. They never dispatch on event kinds.
    static class NpcValues
    {
        public static int Importance(Actor owner, NpcGoalValue value, Guid subject)
        {
            int group = PersonalitySystem.Bias(owner, DecisionKind.Group), compassion = PersonalitySystem.Bias(owner, DecisionKind.Compassion),
                trade = PersonalitySystem.Bias(owner, DecisionKind.Trade), courage = PersonalitySystem.Bias(owner, DecisionKind.Courage),
                law = PersonalitySystem.Bias(owner, DecisionKind.Law);
            RelationshipRecord opinion = owner.Personality.Person(subject);
            int feeling = opinion == null ? 0 : opinion.Feeling, attachment = opinion == null ? 0 : opinion.Attachment,
                fear = opinion == null ? 0 : opinion.Fear, grievance = opinion == null ? 0 : opinion.Grievance;
            switch (value)
            {
                case NpcGoalValue.Nutrition: return 70;
                case NpcGoalValue.Care: return 10 + compassion + Math.Min(0, trade) + feeling / 4 + attachment / 4 + Math.Max(0, group) / 10;
                case NpcGoalValue.Reciprocity: return 25 + compassion + trade + feeling / 2;
                case NpcGoalValue.Safety: return 20 - courage + fear / 2;
                case NpcGoalValue.Justice: return 15 + courage + law + grievance / 2;
                case NpcGoalValue.Belonging: return 20 + group + compassion / 2 + attachment / 2 + feeling / 2;
                case NpcGoalValue.Autonomy: return 40 - group - courage / 2 -
                    Math.Min(2 * Engine.Rules.TRUST_TRUSTING_THRESHOLD, Math.Max(0, owner.TrustInLeader)) * 30 / Engine.Rules.TRUST_TRUSTING_THRESHOLD;
                case NpcGoalValue.Recovery: return 160 + PersonalitySystem.Bias(owner, DecisionKind.Supplies) - courage / 2;
                default: return 0;
            }
        }
        public static NpcGeneratedGoal Evaluate(Actor owner, NpcGoalValue value, Guid subject,
            int current, int desired, int deficit, int confidence, ulong result)
        {
            int importance = Math.Max(0, Math.Min(200, Importance(owner, value, subject)));
            deficit = Math.Max(0, Math.Min(100, deficit)); confidence = Math.Max(0, Math.Min(100, confidence));
            return new NpcGeneratedGoal { Value = value, SubjectId = subject, Current = current, Desired = desired,
                Deficit = deficit, Importance = importance, Confidence = confidence,
                Utility = deficit * importance * confidence / 10000, Result = result,
                EvaluatedTurn = owner.Location.Map.LocalTime.TurnCounter };
        }
    }
}
