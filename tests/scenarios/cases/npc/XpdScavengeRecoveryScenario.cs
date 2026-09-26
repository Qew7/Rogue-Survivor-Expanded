using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdScavengeRecoveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/scavenge-recovery", () => TownScenarioFactory.Arena(4410,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Map home = world.Map;
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "scavenger", false, false, 0);
            leader.Controller = new PlayerController();
            follower.Controller = new CivilianAI();
            AIController ai = (AIController)follower.Controller;
            home.PlaceActorAt(leader, new Point(3, 3));
            home.PlaceActorAt(follower, new Point(2, 2));
            world.SetPlayer(leader);
            leader.AddFollower(follower);
            XpdBase claim = new XpdBase(leader, new[] {
                new Point(1, 1), new Point(2, 1), new Point(1, 2),
                new Point(2, 2), new Point(3, 3)
            });
            home.AddXpdBase(claim);
            claim.SetFoodRoom(new Rectangle(1, 1, 1, 1));
            claim.SetWeaponRoom(new Rectangle(2, 1, 1, 1));

            // The first ground item is on an unreachable tile. No supplies are stored at home.
            ItemFood blocked = new ItemFood(world.Game.GameItems.GROCERIES);
            home.DropItemAt(blocked, new Point(0, 0));
            home.SetTileModelAt(0, 0, world.Game.GameTiles.WALL_BRICK);
            ItemFood loot = new ItemFood(world.Game.GameItems.GROCERIES);
            home.DropItemAt(loot, new Point(5, 2));
            ai.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(home, new Point(2, 2))));
            for (int turn = 0; turn < 50 && ai.Order != null; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
            }
            Check.Equal(true, ai.Order == null, "expedition finishes with empty home storage");
            Check.Equal(true, home.GetItemsAt(new Point(1, 1)) != null &&
                home.GetItemsAt(new Point(1, 1)).Contains(loot), "reachable food is delivered");
            Check.Equal(true, home.GetItemsAt(new Point(0, 0)).Contains(blocked),
                "unreachable food remains on blocked tile");

            // A room assignment may become unusable after it was chosen.
            home.SetTileModelAt(1, 1, world.Game.GameTiles.WALL_BRICK);
            ItemFood second = new ItemFood(world.Game.GameItems.GROCERIES);
            home.DropItemAt(second, new Point(5, 2));
            ai.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(home, new Point(2, 2))));
            for (int turn = 0; turn < 50 && ai.Order != null; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
            }
            Check.Equal(true, ai.Order == null, "blocked storage does not crash or stall expedition");
            Check.Equal(true, home.GetItemsAt(new Point(2, 2)) != null &&
                home.GetItemsAt(new Point(2, 2)).Contains(second),
                "loot falls back to the base order location");
        });
    }
}
