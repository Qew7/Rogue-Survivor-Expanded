using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class StoryGroupSuppliesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-group-supplies", () => TownScenarioFactory.Arena(4625, "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 7, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "organized");
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 2, 1, "sociable");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 1, 2, "generous", "scavenger");
            leader.AddFollower(hungry); leader.AddFollower(collector);
            hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry);
            ItemFood stock = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 };
            world.Map.DropItemAt(stock, new Point(3, 2));
            NpcIntentSupport.Turn(world, leader);
            NpcGroupPlan plan = leader.SocialGroup.Plan;
            Check.Equal(true, plan != null && NpcIntentSupport.HasEvent(collector, "supplies_requested"), "leader assigns a task through real speech");
            NpcIntent gather = NpcIntentSupport.Intent(collector, "gather_group_supplies");
            NpcIntent coordinate = NpcIntentSupport.Intent(leader, "coordinate_group_supplies");
            Check.Equal(plan.StoryId, gather.StoryId, "collector and group plan share an episode");
            int recipientAP = hungry.ActionPoints;
            for (int i = 0; i < 12 && !gather.Finished; i++)
            { world.Map.LocalTime.TurnCounter = i + 1; NpcIntentSupport.Turn(world, collector); }
            Check.Equal(NpcIntentStatus.Completed, gather.Status, "controller fetches, delivers and reports");
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry), "beneficiary actually receives food");
            Check.Equal(2, NpcIntentSupport.FoodUnits(collector), "collector retains the reserve");
            Check.Equal(recipientAP, hungry.ActionPoints, "passive recipient is not charged an action");
            Check.Equal(NpcIntentStatus.Completed, coordinate.Status, "leader's independent goal finishes after the report");
            Check.Equal("completed", plan.Stage, "group plan has a real terminal outcome");
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "supplies_delivered"), "leader hears the delivery report");
            Check.Equal("completed", Session.Get.NpcDirector.Find(plan.StoryId).Stage, "director follows the completed actions");
        });
    }
}
