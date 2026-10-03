using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class GoalFallbackTextScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/goal-fallback-text", () => TownScenarioFactory.Arena(4865,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "wanderer", 1, 1);
            var intent = new NpcIntent(1, "take_breath", owner, owner, 0, 90, 50, 0, null, 0)
                { Plan = new NpcPlan() };
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.Travel });
            Session.Get.ResidentRecords.IntentChanged(owner, intent, "started", null, world.Game.NpcContent);
            Session.Get.ResidentRecords.PlanChanged(owner, intent, world.Game.NpcContent);
            Session.Get.ResidentRecords.IntentChanged(owner, intent, "completed", null, world.Game.NpcContent);
            int readable = 0;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries)
                if ((entry.Kind == "goal_started" || entry.Kind == "goal_plan" || entry.Kind == "goal_completed") &&
                    entry.Text.Contains("take breath") && !entry.Text.Contains("take_breath")) readable++;
            Check.Equal(3, readable, "missing capability names remain readable in every goal record");
        });
    }
}
