using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static class PlannerReplanScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-replan", () => TownScenarioFactory.Arena(4641, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor owner = NpcIntentSupport.Actor(world, "searcher", 1, 1, "solitary", "scavenger");
            var first = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(first, new Point(3, 1));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(5, 1));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ITEMS);
            NpcKnowledgeSystem.Perceive(world.Game, owner, sensor.Sense(world.Game, owner));
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntent goal = NpcStorySystem.StartKnown(owner, new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location }, world.Game.NpcContent.Capability("obtain_food"));
            NpcIntentSupport.Turn(world, owner);
            Check.Equal(new Point(3, 1), goal.Plan.Steps[goal.Plan.Cursor].Place.Position, "first plan selects cheaper stock");
            world.Map.RemoveItemAt(first, new Point(3, 1));
            for (int i = 0; i < 10 && !goal.Finished; i++) { world.Map.LocalTime.TurnCounter = i + 1; NpcIntentSupport.Turn(world, owner); }
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "disappeared stock causes a new successful plan");
            Check.Equal(3, NpcIntentSupport.FoodUnits(owner), "only remaining physical food is acquired");
            Check.Equal(null, world.Map.GetItemsAt(5, 1), "replacement stock is actually removed");
            int plans = 0; foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(owner).Entries) if (entry.Kind == "goal_plan") plans++;
            Check.Equal(true, plans >= 2, "chronicle records changed plans without pretending the first succeeded");
            Check.Equal(true, goal.Plan.Expanded <= 128, "planning work stays bounded");
        });
    }
}
