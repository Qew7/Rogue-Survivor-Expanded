using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdAutonomousSupplyReachabilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/autonomous-supply-reachability", () => TownScenarioFactory.Arena(4431,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            spectator.Controller = new PlayerController();
            world.Place(spectator, 6, 4);
            world.SetPlayer(spectator);
            Actor collector = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "collector", false, false, 0);
            collector.Controller = new CivilianAI();
            world.Map.PlaceActorAt(collector, new Point(2, 2));
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            world.Map.PlaceActorAt(follower, new Point(0, 4));
            collector.AddFollower(follower);
            XpdBase home = new XpdBase(collector, new[] {
                new Point(1, 1), new Point(2, 1), new Point(2, 2) });
            home.SetFoodRoom(new Rectangle(1, 1, 1, 1));
            world.Map.AddXpdBase(home);

            ItemFood blocked = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(blocked, new Point(0, 0));
            world.Map.SetTileModelAt(0, 0, world.Game.GameTiles.WALL_BRICK);
            ItemFood reachable = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(reachable, new Point(5, 2));
            world.Map.LocalTime.TurnCounter = 26; // collector x+y=4: autonomous search turn.
            for (int turn = 0; turn < 55; turn++)
            {
                collector.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(collector);
                Inventory stored = world.Map.GetItemsAt(new Point(1, 1));
                if (stored != null && stored.Contains(reachable)) break;
            }
            Check.Equal(true, world.Map.GetItemsAt(new Point(1, 1)) != null &&
                world.Map.GetItemsAt(new Point(1, 1)).Contains(reachable),
                "autonomous NPC skips blocked first supply and returns reachable food");
            Check.Equal(true, world.Map.GetItemsAt(new Point(0, 0)).Contains(blocked),
                "unreachable food remains untouched");
        });
    }
}
