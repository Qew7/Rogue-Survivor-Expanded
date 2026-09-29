using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    enum NpcGoalValue { Nutrition, Care, Reciprocity, Safety, Justice, Belonging, Autonomy, Recovery, MedicalCare, Commitment, Restitution, Possession, ProtectHome }
    [Serializable]
    sealed class NpcGeneratedGoal
    {
        public NpcGoalValue Value;
        [System.Runtime.Serialization.OptionalField] public string DefinitionId;
        [System.Runtime.Serialization.OptionalField] public string DefinitionDescription;
        [System.Runtime.Serialization.OptionalField] public string IdentitySuffix;
        public Guid SubjectId;
        public int Current, Desired, Deficit, Importance, Confidence, Utility, EvaluatedTurn;
        public ulong Result;
        [System.Runtime.Serialization.OptionalField] public NpcPlanningResult ExtendedResult;
        public NpcPlanningState ResultState
        { get { return (NpcPlanningState)Result | (ExtendedResult == null ? default(NpcPlanningState) : ExtendedResult.State); }
            set { Result = value.Low; ExtendedResult = NpcPlanningResult.Extra(value); } }
        public string Resource;
        public long ObligationId;
        public int ModelId = -1;
        public Location ObjectPlace;
        public Guid ItemId;
        public long[] Causes;
        public string Key { get { return (DefinitionId ?? Value.ToString()) + ":" + SubjectId.ToString("N") + (Resource == null ? "" : ":" + Resource) +
            (ObligationId == 0 ? "" : ":" + ObligationId) + KeySuffix; } }
        string KeySuffix
        {
            get
            {
                if (IdentitySuffix != null) return IdentitySuffix;
                var definition = Gameplay.Personality.NpcContentCatalog.Default.Value(this);
                return definition == null || definition.IdentitySuffix == null ? "" : definition.IdentitySuffix(this);
            }
        }
        public string Description
        {
            get
            {
                var definition = Gameplay.Personality.NpcContentCatalog.Default.Value(Value);
                return DefinitionDescription ?? (definition == null ? Value.ToString() : definition.Description);
            }
        }
        public string Explanation { get { return (DefinitionId ?? Value.ToString()) + ": " + Current + " → " + Desired +
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
        internal void RememberGoalCooldown(string key, int until)
        {
            if (m_GoalCooldowns == null) m_GoalCooldowns = new Dictionary<string, int>();
            if (m_GoalCooldowns.Count >= 64)
            {
                string oldest = null; int earliest = Int32.MaxValue;
                foreach (var pair in m_GoalCooldowns) if (pair.Value < earliest) { earliest = pair.Value; oldest = pair.Key; }
                if (oldest != null) m_GoalCooldowns.Remove(oldest);
            }
            m_GoalCooldowns[key] = until;
        }
    }
}
