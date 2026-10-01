using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcMedicalRecovery
    {
        public static void MedicineUsed(RogueGame game, Actor owner, int previousHP, NpcIntent performedGoal = null)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.HitPoints <= previousHP) return;
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && (performedGoal != null ? intent == performedGoal :
                    intent.Plan != null && intent.Plan.DesiredState.Contains((ulong)NpcPlanFact.Healthy)))
                {
                    NpcPlanExecution.Publish(game, "treated_wounds", owner, null, intent);
                    if (owner.HitPoints >= game.Rules.ActorMaxHPs(owner)) new NpcActionContext(game, owner, intent).Done(default(NpcPlanningState), retain: false);
                    else if (intent.Plan != null) { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = owner.Location.Map.LocalTime.TurnCounter; }
                    break;
                }
        }
    }
}
