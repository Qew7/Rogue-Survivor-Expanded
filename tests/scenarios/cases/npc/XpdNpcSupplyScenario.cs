using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class XpdNpcSupplyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/npc-supply", () => TownScenarioFactory.Arena(4417,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor spectator = SkillScenario.Actor(world);
            world.SetPlayer(spectator);
            Actor collector = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "collector", false, false, 0);
            collector.Controller = new CivilianAI();
            world.Map.PlaceActorAt(collector, new Point(2, 2));
            XpdBase home = new XpdBase(collector,
                new[] { new Point(1, 1), new Point(2, 1), new Point(1, 2), new Point(2, 2) });
            home.SetFoodRoom(new Rectangle(1, 1, 1, 1));
            world.Map.AddXpdBase(home);
            ItemFood loot = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(loot, new Point(5, 2));
            world.Map.LocalTime.TurnCounter = 26; // x + y = 4, so this is the supply-search turn.
            AIController ai = (AIController)collector.Controller;
            for (int turn = 0; turn < 50; turn++)
            {
                collector.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(collector);
                Inventory stored = world.Map.GetItemsAt(new Point(1, 1));
                if (stored != null && stored.Contains(loot)) break;
            }
            Check.Equal(true, world.Map.GetItemsAt(new Point(1, 1)) != null &&
                world.Map.GetItemsAt(new Point(1, 1)).Contains(loot),
                "uncommanded NPC brings food into faction storage");
            Check.Equal(null, ai.Order, "autonomous supply trip ends");

            world.Map.LocalTime.TurnCounter = 28; // x + y = 2; no new trip without outside loot.
            collector.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(collector);
            Check.Equal(true, world.Map.GetItemsAt(new Point(1, 1)) != null &&
                world.Map.GetItemsAt(new Point(1, 1)).Contains(loot),
                "stored food remains in base after next NPC turn");
            Check.Equal(null, ai.Order, "NPC stays idle when no outside supplies remain");
        });
    }
}
