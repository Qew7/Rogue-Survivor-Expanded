using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    enum NpcGoalValue { Nutrition, Care, Reciprocity, Safety, Justice, Belonging, Autonomy, Recovery, MedicalCare, Commitment, Restitution, Possession, ProtectHome }
    [Serializable]
    sealed class NpcGeneratedGoal
    {
        public NpcGoalValue Value;
        public Guid SubjectId;
        public int Current, Desired, Deficit, Importance, Confidence, Utility, EvaluatedTurn;
        public ulong Result;
        public string Resource;
        public long ObligationId;
        public int ModelId = -1;
        public Location ObjectPlace;
        public Guid ItemId;
        public long[] Causes;
        public string Key { get { return Value + ":" + SubjectId.ToString("N") + (Resource == null ? "" : ":" + Resource) +
            (ObligationId == 0 ? "" : ":" + ObligationId) + (Value == NpcGoalValue.Possession ? ":" + ModelId + ":" + ItemId.ToString("N") : ""); } }
        public string Description
        {
            get
            {
                switch (Value)
                {
                    case NpcGoalValue.Nutrition: return "Have usable food";
                    case NpcGoalValue.Recovery: return "Recover health";
                    case NpcGoalValue.Care: return "Provide needed food";
                    case NpcGoalValue.Reciprocity: return "Reduce a personal debt";
                    case NpcGoalValue.Safety: return "Reach safety";
                    case NpcGoalValue.Justice: return "Communicate a boundary";
                    case NpcGoalValue.Belonging: return "Restore contact";
                    case NpcGoalValue.MedicalCare: return "Meet a person's medical need";
                    case NpcGoalValue.Commitment: return "Fulfil an outstanding promise";
                    case NpcGoalValue.Restitution: return "Replace supplies lost through my actions";
                    case NpcGoalValue.Possession: return "Recover a valued kind of item";
                    case NpcGoalValue.ProtectHome: return "Return to threatened shelter";
                    default: return "Leave an unsafe group";
                }
            }
        }
        public string Explanation { get { return Value + ": " + Current + " → " + Desired +
            "; deficit " + Deficit + ", importance " + Importance + ", confidence " + Confidence + ", utility " + Utility; } }
    }
    sealed partial class PersonalityState
    {
        Dictionary<string, int> m_GoalCooldowns;
        internal bool CanGenerateGoal(string key, int turn)
        {
            if (!GoalCooldownReady(key, turn)) return false;
            int active = 0;
            foreach (NpcIntent intent in IntentList)
                if (!intent.Finished) { active++; if (intent.Generated != null && intent.Generated.Key == key) return false; }
            return active < 4;
        }
        internal bool GoalCooldownReady(string key, int turn)
        { int until; return m_GoalCooldowns == null || !m_GoalCooldowns.TryGetValue(key, out until) || turn >= until; }
        internal bool LegacyCooldownReady(string definition, int turn)
        { int until; return m_IntentCooldowns == null || !m_IntentCooldowns.TryGetValue(definition, out until) || turn >= until; }
        internal NpcIntent StartGeneratedGoal(string id, Actor owner, NpcKnownPerson target,
            NpcGeneratedGoal goal, int turn, int duration, int cooldown, long cause, string story)
        {
            if (!CanGenerateGoal(goal.Key, turn)) return null;
            if (m_GoalCooldowns == null) m_GoalCooldowns = new Dictionary<string, int>();
            if (m_GoalCooldowns.Count >= 64)
            {
                string oldest = null; int earliest = Int32.MaxValue;
                foreach (var pair in m_GoalCooldowns) if (pair.Value < earliest) { earliest = pair.Value; oldest = pair.Key; }
                if (oldest != null) m_GoalCooldowns.Remove(oldest);
            }
            m_GoalCooldowns[goal.Key] = turn + cooldown;
            var intent = new NpcIntent(++m_IntentSequence, id, owner, target, turn, duration, goal.Utility, cause, story, 0) { Generated = goal };
            IntentList.Add(intent);
            while (IntentList.Count > 12)
            { int index = IntentList.FindIndex(i => i.Finished); if (index < 0) break; IntentList.RemoveAt(index); }
            return intent;
        }
    }
}
