using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerRiskScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-risk", () => TownScenarioFactory.Arena(4647, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); Actor owner = NpcIntentSupport.Player(world, 8, 2);
            Actor lawful = NpcIntentSupport.Actor(world, "lawful", 4, 1, "lawful");
            Actor rebel = NpcIntentSupport.Actor(world, "rebel", 1, 1, "rebellious");
            Actor helper = NpcIntentSupport.Actor(world, "helper", 4, 2, "generous"); NpcIntentSupport.Food(world, helper, 3);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1), new Point(5, 1) }));
            foreach (int x in new[] { 2, 5 }) world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, new Point(x, 1));
            foreach (Actor actor in new[] { lawful, rebel })
            {
                actor.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
                NpcStorySystem.StartKnown(actor, new NpcKnownPerson { Id = actor.PersonalityIdentity, Name = actor.UnmodifiedName, Place = actor.Location }, world.Game.NpcContent.Capability("obtain_food"));
            }
            NpcIntentSupport.Turn(world, lawful);
            NpcIntent honestGoal = NpcIntentSupport.Intent(lawful, "obtain_food");
            Check.Equal(NpcPlanAction.AskFood, honestGoal.Plan.Steps[0].Action, "lawfulness prices known ownership risk into choosing assistance");
            Check.Equal(0, NpcIntentSupport.FoodUnits(lawful), "lawful actor does not silently acquire foreign supplies");
            NpcIntentSupport.Turn(world, rebel);
            Check.Equal(3, NpcIntentSupport.FoodUnits(rebel), "rebellious actor actually chooses the cheaper foreign pickup");
            Check.Equal(true, NpcIntentSupport.HasEvent(rebel, "base_theft"), "real ownership rules produce the theft consequence");
            NpcIntentSupport.Turn(world, helper);
            Check.Equal(1, NpcIntentSupport.FoodUnits(lawful), "alternative plan still achieves the same food goal through a real gift");
            Check.Equal(NpcIntentStatus.Completed, honestGoal.Status, "observed assistance confirms success");
        });
    }
}
