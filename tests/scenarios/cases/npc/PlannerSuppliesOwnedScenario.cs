using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerSuppliesOwnedScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-owned-supplies", () => TownScenarioFactory.Arena(4643, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 0, "loyal");
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 2, 1, "generous");
            leader.AddFollower(collector); leader.AddFollower(hungry);
            NpcIntentSupport.Food(world, collector, 3); hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntent goal = NpcStorySystem.StartKnown(collector, new NpcKnownPerson { Id = hungry.PersonalityIdentity, Name = hungry.UnmodifiedName, Place = hungry.Location },
                world.Game.NpcContent.Capability("gather_group_supplies"), destination: new Location(world.Map, new Point(5, 1)), groupId: leader.SocialGroup.Identity);
            goal.CoordinatorId = leader.PersonalityIdentity; goal.CoordinatorPlace = leader.Location;
            NpcIntentSupport.Turn(world, collector);
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry), "owned supplies are delivered immediately");
            Check.Equal(NpcPlanAction.GiveFood, goal.Plan.Steps[0].Action, "planner omits needless pickup and travel to assigned stock");
            Check.Equal(false, NpcIntentSupport.HasEvent(collector, "supplies_acquired"), "skipped step has no fictional success event");
            Check.Equal(false, goal.Finished, "delivery and reporting remain distinct actual outcomes");
            NpcIntentSupport.Turn(world, collector);
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "actual report completes the goal");
            Check.Equal(2, NpcIntentSupport.FoodUnits(collector), "no stock is cloned");
        });
    }
}
