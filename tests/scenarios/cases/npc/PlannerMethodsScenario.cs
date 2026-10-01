using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class PlannerMethodsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-methods", () => TownScenarioFactory.Arena(4640, "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 9, 2);
            Actor social = NpcIntentSupport.Actor(world, "social", 1, 1, "sociable");
            Actor solitary = NpcIntentSupport.Actor(world, "solitary", 5, 1, "solitary", "scavenger");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 2, 1, "generous"); NpcIntentSupport.Food(world, helper, 3);
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(3, 1));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ITEMS);
            foreach (Actor owner in new[] { social, solitary })
            {
                owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
                NpcKnowledgeSystem.Perceive(world.Game, owner, sensor.Sense(world.Game, owner));
                NpcStorySystem.StartKnown(world.Game.NpcContent, owner, new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location }, world.Game.NpcContent.Capability("obtain_food"));
            }
            NpcIntentSupport.Turn(world, social);
            NpcIntent a = NpcIntentSupport.Intent(social, "obtain_food"), b = NpcIntentSupport.Intent(solitary, "obtain_food");
            Check.Equal(NpcPlanAction.AskFood, a.Plan.Steps[0].Action, "social trait selects assistance for the food goal");
            Check.Equal(0, NpcIntentSupport.FoodUnits(social), "predicted help is not actual food");
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(social), "helper's independent controller performs the gift");
            Check.Equal(NpcIntentStatus.Completed, a.Status, "goal completes on actual assistance");
            NpcIntentSupport.Turn(world, solitary);
            Check.Equal(NpcPlanAction.Travel, b.Plan.Steps[0].Action, "solitary scavenger selects travelling to known stock");
            NpcIntentSupport.Turn(world, solitary);
            Check.Equal(3, NpcIntentSupport.FoodUnits(solitary), "same desired state is achieved by actual pickup");
            Check.Equal(NpcIntentStatus.Completed, b.Status, "pickup completes the same kind of goal");
        });
    }
}
