using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryGroupBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-group-boundaries", () => TownScenarioFactory.Arena(4630, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 0, 1, "loyal");
            Actor solitary = NpcIntentSupport.Actor(world, "solitary", 1, 1, "solitary");
            Actor collector = NpcIntentSupport.Actor(world, "collector", 0, 2, "generous");
            leader.AddFollower(solitary); leader.AddFollower(collector);
            Location shelter = new Location(world.Map, new Point(3, 1)); world.Map.GetTileAt(3, 1).IsInside = true;
            var proposal = new NpcGroupPlan { Kind = "group_shelter", CollectorId = leader.PersonalityIdentity, Destination = shelter, Deadline = 180 };
            Check.Equal(true, world.Try(new ActionNpcGroupPlan(leader, world.Game, collector, proposal)), "real leader proposes shelter");
            Check.Equal(null, NpcIntentSupport.Intent(solitary, "seek_group_shelter"), "solitary member independently rejects the goal");
            NpcIntentSupport.Turn(world, solitary);
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "shelter_declined"), "rejection is actually spoken to the leader");
            NpcKnownPerson recipient = new NpcKnownPerson { Id = solitary.PersonalityIdentity, Name = solitary.UnmodifiedName, Place = solitary.Location };
            NpcIntent gather = NpcStorySystem.StartKnown(collector, recipient, NpcIntentContent.Gather, destination: new Location(world.Map, new Point(2, 2)), groupId: leader.SocialGroup.Identity);
            var stock = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }; world.Map.DropItemAt(stock, new Point(2, 2));
            world.Place(collector, 1, 2);
            var take = new ActionNpcStory(collector, world.Game, gather, food: stock);
            Check.Equal(true, take.IsLegal(), "remembered supplies can be taken on arrival");
            world.Map.RemoveItemAt(stock, new Point(2, 2));
            int ap = collector.ActionPoints; take.Perform();
            Check.Equal(false, take.IsLegal(), "stale resource reference cannot fabricate supplies");
            Check.Equal(ap, collector.ActionPoints, "failed stale action costs no AP");
            Check.Equal(0, NpcIntentSupport.FoodUnits(collector), "missing stock is not cloned into inventory");
            leader.RemoveFollower(collector);
            Check.Equal(false, new ActionNpcStory(collector, world.Game, gather, food: stock).IsLegal(), "former member cannot execute the group's assignment");
        });
    }
}
