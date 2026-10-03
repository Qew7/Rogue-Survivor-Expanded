using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class OperatorArchiveTextScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/operator-archive-text", () => TownScenarioFactory.Arena(4844,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "helper", 1, 1);
            Actor target = NpcIntentSupport.Actor(world, "patient", 2, 1);
            var intent = new NpcIntent(1, "medical_aid", owner, target, 0, 90, 50, 0, null, 0)
                { Plan = new NpcPlan() };
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.AskMedicine,
                OperatorId = "medicine.ask" });
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.TreatPerson,
                OperatorId = "medicine.treat" });
            Session.Get.ResidentRecords.PlanChanged(owner, intent, world.Game.NpcContent);
            ResidentEntry plan = null;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries)
                if (entry.Kind == "goal_plan") plan = entry;
            Check.Equal(true, plan != null && plan.Text.Contains("ask for medicine, then treat someone"),
                "archived plan uses the registered operator wording");
            Check.Equal("ask for medicine", world.Game.NpcContent.Operator("medicine.ask").ArchiveText(intent.Plan.Steps[0]),
                "the operator definition owns its archive phrase");
        });
    }
}
