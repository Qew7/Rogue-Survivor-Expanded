using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
static class GroupMedicineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-medicine", () => TownScenarioFactory.Arena(4806,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "healer");
            Actor patient = NpcIntentSupport.Actor(world, "patient", 2, 1, "sociable");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 1, 2, "kind");
            leader.AddFollower(patient); leader.AddFollower(collector); patient.HitPoints = 1;
            NpcIntentSupport.Turn(world, patient);
            world.Map.DropItemAt(new ItemMedicine(world.Game.GameItems.MEDIKIT) { Quantity = 2 }, new Point(3, 2));
            NpcIntentSupport.Turn(world, leader);
            NpcGroupPlan plan = leader.SocialGroup.Plan;
            Check.Equal(true, plan != null && plan.Kind == "group_medicine", "group chooses real medical need rather than a food template");
            NpcIntent task = NpcIntentSupport.Intent(collector, "gather_group_medicine");
            Check.Equal(true, task != null && task.StoryId == plan.StoryId, "collector accepts an independent medical goal");
            int previous = patient.Inventory.CountItems;
            for (int t = 1; t < 20 && !task.Finished; t++) { world.Map.LocalTime.TurnCounter = t; NpcIntentSupport.Turn(world, collector); }
            Check.Equal(NpcIntentStatus.Completed, task.Status, "collector really fetches, delivers and reports medicine");
            Check.Equal(previous + 1, patient.Inventory.CountItems, "medical task transfers an actual item");
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "supplies_delivered"), "leader receives a causal completion report");
        });
    }
}
