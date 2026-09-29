using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcMedicalRecovery
    {
        public static void MedicineUsed(RogueGame game, Actor owner, int previousHP)
        {
            if (!NpcIntentSystem.Enabled(owner) || owner.HitPoints <= previousHP) return;
            foreach (NpcIntent intent in owner.Personality.Intents)
                if (!intent.Finished && intent.DefinitionId == NpcIntentContent.Recover.Id)
                {
                    NpcPlanExecution.Publish(game, "treated_wounds", owner, null, intent);
                    if (owner.HitPoints >= game.Rules.ActorMaxHPs(owner)) NpcIntentSystem.Finish(owner, intent, NpcIntentStatus.Completed, "actually recovered health");
                    else if (intent.Plan != null) { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = owner.Location.Map.LocalTime.TurnCounter; }
                    break;
                }
        }
    }
}
