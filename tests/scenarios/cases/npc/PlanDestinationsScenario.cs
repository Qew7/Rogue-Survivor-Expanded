using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlanDestinationsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/plan-destinations", () => TownScenarioFactory.Arena(4943,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "helper", 1, 1, "kind");
            Actor patient = NpcIntentSupport.Actor(world, "patient", 2, 1);
            var pharmacy = new Zone("Pharmacy", new Rectangle(3, 1, 1, 1))
                { BuildingKind = BuildingKind.Pharmacy };
            world.Map.AddZone(pharmacy);
            Map baseMap = new Map(4944, "XPD base@A0", 3, 3);
            world.Map.District.AddUniqueMap(baseMap);
            NpcGeneratedGoal need = NpcValues.Evaluate(world.Game.NpcContent, owner,
                world.Game.NpcContent.Value(NpcGoalValue.MedicalCare), patient.PersonalityIdentity,
                0, 100, 100, 100, 0);
            NpcKnownPerson target = new NpcKnownPerson { Id = patient.PersonalityIdentity,
                Name = patient.UnmodifiedName, Place = patient.Location, SeenTurn = 0 };
            NpcIntent intent = NpcGoalLifecycle.Start(world.Game.NpcContent, owner, target,
                world.Game.NpcContent.Capability("medical_aid"), generated: need);
            Check.Equal(true, intent != null, "a real medical goal starts");
            intent.Plan = new NpcPlan();
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.Travel,
                Place = new Location(world.Map, new Point(3, 1)) });
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.Travel,
                Place = new Location(baseMap, new Point(1, 1)) });
            intent.Plan.Steps.Add(new NpcPlanStep { Action = NpcPlanAction.Travel });
            Session.Get.ResidentRecords.PlanChanged(owner, intent);
            ResidentEntry plan = null;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries)
                if (entry.Kind == "goal_plan") plan = entry;
            Check.Equal(true, plan != null && plan.Text.Contains("the pharmacy in district A0"),
                "travel to a known building names the building and district");
            Check.Equal(true, plan.Text.Contains("XPD base in district A0"),
                "travel to a base names the map and district");
            Check.Equal(true, plan.Text.Contains("travel onward") && !plan.Text.Contains("the destination"),
                "a missing place does not pretend to know the destination");
        });
    }
}
