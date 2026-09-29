using System.Runtime.Serialization;
namespace djack.RogueSurvivor.Data
{
    sealed partial class PersonalityState
    {
        [OptionalField] NpcGroupPlan m_FactionPlan;
        [OptionalField] long m_FactionPlanSequence;
        [OptionalField] int m_NextFactionPlanTurn;
        public NpcGroupPlan FactionPlan { get { return m_FactionPlan; } set { m_FactionPlan = value; } }
        public long NextFactionPlanSequence() { return ++m_FactionPlanSequence; }
        public int NextFactionPlanTurn { get { return m_NextFactionPlanTurn; } set { m_NextFactionPlanTurn = value; } }
    }
}
