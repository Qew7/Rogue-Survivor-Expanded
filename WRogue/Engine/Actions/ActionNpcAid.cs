using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcAid : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        readonly Actor target;
        ItemMedicine medicine;
        public ActionNpcAid(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step, Actor target) : base(actor, game)
        { this.goal = goal; this.step = step; this.target = target; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step || target == null || target == m_Actor ||
                target.PersonalityIdentity != step.Target || target.IsSleeping || target.Model.Abilities.IsUndead || !target.Model.Abilities.IsIntelligent ||
                !NpcIntentSystem.CanSee(m_Game, m_Actor, target) || !NpcPlanExecution.Near(m_Game, m_Actor, target.Location) || m_Game.Rules.AreEnemies(m_Actor, target)) return false;
            if (step.Action == NpcPlanAction.AskMedicine)
                return !m_Actor.Personality.Knowledge.WasTold(-goal.Sequence, target.PersonalityIdentity) && NpcPlanExecution.Medicine(m_Game, m_Actor, m_Actor.Location, true) == null;
            if (step.Action != NpcPlanAction.GiveMedicine && step.Action != NpcPlanAction.TreatPerson) return false;
            medicine = NpcPlanExecution.Medicine(m_Game, m_Actor, m_Actor.Location, true);
            if (medicine == null) return false;
            if (step.Action == NpcPlanAction.TreatPerson) return target.HitPoints < m_Game.Rules.ActorMaxHPs(target);
            string reason; var gift = new ItemMedicine(medicine.Model);
            return target.Inventory != null && target.Inventory != m_Actor.Inventory && target.Inventory.CanAddAll(gift) && m_Game.Rules.CanActorGiveItemTo(m_Actor, target, gift, out reason);
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            if (step.Action == NpcPlanAction.AskMedicine)
            {
                m_Game.DoSay(m_Actor, target, "Could you spare medicine? I'm trying to get treatment.", RogueGame.Sayflags.NONE);
                int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
                m_Actor.Personality.Knowledge.Told(-goal.Sequence, target.PersonalityIdentity, turn);
                NpcPlanExecution.Publish(m_Game, "requested_medicine", m_Actor, target, goal);
                goal.Plan.Invalidate(); goal.NextAttempt = turn + 8; return;
            }
            m_Game.DoNpcMedicalAid(m_Actor, target, medicine, step.Action == NpcPlanAction.TreatPerson, goal);
        }
    }
}
