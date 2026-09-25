using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdScavengeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/follower-scavenge", () => TownScenarioFactory.Arena(4402,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Map home = world.Map;
            Map annex = new Map(4403, "annex", 5, 3);
            for (int y = 0; y < annex.Height; y++)
                for (int x = 0; x < annex.Width; x++)
                    annex.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            home.District.AddUniqueMap(annex);
            home.SetExitAt(new Point(5, 2), new Exit(annex, new Point(1, 1)) { IsAnAIExit = true });
            annex.SetExitAt(new Point(1, 1), new Exit(home, new Point(5, 2)) { IsAnAIExit = true });

            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "scavenger", false, false, 0);
            leader.Controller = new PlayerController();
            follower.Controller = new CivilianAI();
            AIController scavengerAI = (AIController)follower.Controller;
            home.PlaceActorAt(leader, new Point(3, 3));
            home.PlaceActorAt(follower, new Point(2, 2));
            world.SetPlayer(leader);
            leader.AddFollower(follower);
            XpdBase baseClaim = new XpdBase(leader, new[] {
                new Point(1, 1), new Point(2, 1), new Point(1, 2), new Point(2, 2),
                new Point(3, 1), new Point(3, 2), new Point(3, 3)
            });
            home.AddXpdBase(baseClaim);
            baseClaim.SetFoodRoom(new Rectangle(1, 1, 1, 1));
            baseClaim.SetWeaponRoom(new Rectangle(2, 1, 1, 1));
            ItemFood ration = new ItemFood(world.Game.GameItems.GROCERIES);
            ItemMeleeWeapon travelWeapon = new ItemMeleeWeapon(world.Game.GameItems.CROWBAR);
            ItemFood loot = new ItemFood(world.Game.GameItems.GROCERIES);
            home.DropItemAt(ration, new Point(1, 1));
            home.DropItemAt(travelWeapon, new Point(2, 1));
            annex.DropItemAt(loot, new Point(3, 1));
            scavengerAI.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(home, new Point(2, 2))));

            bool tookRation = false, tookWeapon = false, visitedAnnex = false;
            for (int turn = 0; turn < 70 && scavengerAI.Order != null; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
                tookRation |= follower.Inventory.Contains(ration);
                tookWeapon |= follower.Inventory.Contains(travelWeapon);
                visitedAnnex |= follower.Location.Map == annex;
            }
            Check.Equal(true, tookRation, "follower takes travel food from storage");
            Check.Equal(true, tookWeapon, "follower takes travel weapon from storage");
            Check.Equal(true, visitedAnnex, "follower crosses to neighboring map");
            Check.Equal(true, scavengerAI.Order == null, "expedition finishes");
            Check.Equal(true, home.GetItemsAt(new Point(1, 1)).Contains(loot),
                "scavenged food is stored in the assigned room");
            Check.Equal(false, follower.Inventory.Contains(loot), "loot is removed from inventory");

            ItemMeleeWeapon weaponLoot = new ItemMeleeWeapon(world.Game.GameItems.BASEBALLBAT);
            annex.DropItemAt(weaponLoot, new Point(3, 1));
            scavengerAI.SetOrder(new ActorOrder(ActorTasks.SCAVENGE_SUPPLIES,
                new Location(home, new Point(2, 2))));
            for (int turn = 0; turn < 70 && scavengerAI.Order != null; turn++)
            {
                follower.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(follower);
            }
            Check.Equal(true, scavengerAI.Order == null, "weapon expedition finishes");
            Check.Equal(true, home.GetItemsAt(new Point(2, 1)) != null &&
                home.GetItemsAt(new Point(2, 1)).Contains(weaponLoot),
                "scavenged weapon returns to the assigned weapon room");
        });
    }
}
