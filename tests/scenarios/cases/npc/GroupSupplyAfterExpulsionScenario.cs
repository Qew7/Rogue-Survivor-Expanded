using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class GroupSupplyAfterExpulsionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-supply-after-expulsion", () => TownScenarioFactory.Arena(4823,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "organized");
            Actor beneficiary = NpcIntentSupport.Actor(world, "beneficiary", 2, 1);
            Actor collector = NpcIntentSupport.Actor(world, "collector", 1, 2, "generous", "scavenger");
            leader.AddFollower(beneficiary); leader.AddFollower(collector);
            var visible = new List<Actor> { beneficiary, collector };
            NpcKnownPerson known = leader.Personality.Knowledge.See(beneficiary, 0);
            known.FoodNeed = 100; known.FoodNeedTurn = 0;
            known.Violation = known.ViolationConfidence = 100;
            Location food = new Location(world.Map, new Point(3, 2));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }, food.Position);
            leader.Personality.Knowledge.RememberPlace(new NpcKnownPlace(food, "food", 0, 3, 0));
            Check.Equal(null, NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible),
                "leader does not start routine food supply for a member marked for expulsion");

            known.MedicalNeed = 100; known.MedicalTurn = 0;
            Location medicine = new Location(world.Map, new Point(4, 2));
            world.Map.DropItemAt(new ItemMedicine(world.Game.GameItems.MEDIKIT) { Quantity = 2 }, medicine.Position);
            leader.Personality.Knowledge.RememberPlace(new NpcKnownPlace(medicine, "medicine", 0, 2, 0));
            NpcGroupPlan urgent = NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible);
            Check.Equal("group_medicine", urgent.Kind, "urgent medical help remains available to a distrusted member");

            known.MedicalNeed = 0; known.Violation = 0;
            NpcGroupPlan plan = NpcStorySystem.ProposeGroupPlan(world.Game, leader, visible);
            Check.Equal("group_supplies", plan.Kind, "ordinary food task becomes available after the violation resolves");
            Check.Equal(true, world.Try(new ActionNpcGroupPlan(leader, world.Game, collector, plan)),
                "leader issues a real group task");
            NpcIntent task = NpcIntentSupport.Intent(collector, GroupsModule.GatherSuppliesId);
            Check.Equal(true, task != null && !task.Finished, "collector accepts the task");

            leader.RemoveFollower(beneficiary);
            NpcIntentSupport.Turn(world, collector);
            Check.Equal(NpcIntentStatus.Abandoned, task.Status, "collector drops the task when its beneficiary leaves");
            world.Map.LocalTime.TurnCounter = 1;
            NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal("failed", plan.Stage, "group plan no longer remains active for an ex-member");
        });
    }
}
