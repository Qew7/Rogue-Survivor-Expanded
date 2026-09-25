using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class XpdFollowerTheftScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/follower-theft", () => TownScenarioFactory.Arena(4421,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor thief = SkillScenario.Actor(world);
            world.Place(thief, 1, 1);
            world.SetPlayer(thief);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "owner", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "guard", false, false, 0);
            world.Map.PlaceActorAt(leader, new Point(5, 1));
            world.Map.PlaceActorAt(follower, new Point(2, 1));
            leader.AddFollower(follower);
            world.Map.AddXpdBase(new XpdBase(leader, new[] { new Point(1, 1) }));
            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.WALL_BRICK);
            ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(food, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game,
                new Point(1, 1), food)), "follower watches theft");
            Check.Equal(true, world.Game.Rules.AreEnemies(thief, follower),
                "follower identifies thief without leader seeing");
        });
    }
}
