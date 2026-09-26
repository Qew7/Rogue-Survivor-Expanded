using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdScavengeRedirectScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/scavenge-redirect", () => TownScenarioFactory.Arena(4415,
            ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Map oldHome = world.Map;
            District nextDistrict = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
            Map newHome = new Map(4416, "new home", 7, 4);
            for (int y = 0; y < newHome.Height; y++)
                for (int x = 0; x < newHome.Width; x++)
                    newHome.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            nextDistrict.EntryMap = newHome;
            World expanded = new World(2);
            expanded[0, 0] = oldHome.District;
            expanded[1, 0] = nextDistrict;
            Session.Get.World = expanded;
            oldHome.SetExitAt(new Point(6, 1), new Exit(newHome, new Point(0, 1)) { IsAnAIExit = true });
            newHome.SetExitAt(new Point(0, 1), new Exit(oldHome, new Point(6, 1)) { IsAnAIExit = true });

            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "scavenger", false, false, 0);
            leader.Controller = new PlayerController();
            follower.Controller = new CivilianAI();
            AIController ai = (AIController)follower.Controller;
            oldHome.PlaceActorAt(leader, new Point(1, 1));
            oldHome.PlaceActorAt(follower, new Point(2, 2));
            world.SetPlayer(leader);
            leader.AddFollower(follower);
            XpdBase oldBase = new XpdBase(leader,
                new[] { new Point(1, 1), new Point(2, 1), new Point(2, 2) });
            oldHome.AddXpdBase(oldBase);
            ItemFood loot = new ItemFood(world.Game.GameItems.GROCERIES);
            oldHome.DropItemAt(loot, new Point(5, 1));
            ai.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(oldHome, new Point(2, 2))));
            for (int turn = 0; turn < 30 && !follower.Inventory.Contains(loot); turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
            }
            Check.Equal(true, follower.Inventory.Contains(loot), "scavenger has loot before move");

            oldHome.RemoveXpdBase(oldBase);
            XpdBase newBase = new XpdBase(leader,
                new[] { new Point(2, 1), new Point(2, 2) });
            newBase.SetFoodRoom(new Rectangle(2, 1, 1, 1));
            newHome.AddXpdBase(newBase);
            follower.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(follower);
            Check.Equal(true, ai.Order != null && ai.Order.Location.Map == newHome,
                "existing order is retargeted without losing carried loot");
            for (int turn = 0; turn < 60 && ai.Order != null; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
            }
            Check.Equal(true, ai.Order == null, "redirected expedition finishes");
            Check.Equal(true, newHome.GetItemsAt(new Point(2, 1)) != null &&
                newHome.GetItemsAt(new Point(2, 1)).Contains(loot),
                "loot reaches the new base across district boundary");
            Check.Equal(false, follower.Inventory.Contains(loot), "loot leaves scavenger inventory");

            ai.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(oldHome, new Point(2, 2))));
            newHome.RemoveXpdBase(newBase);
            follower.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(follower);
            Check.Equal(null, ai.Order, "order ends if no replacement base exists");
        });
    }
}
