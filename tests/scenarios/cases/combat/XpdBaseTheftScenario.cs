using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class XpdBaseTheftScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-theft", () => TownScenarioFactory.Arena(4418,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "thief", false, false, 0);
            player.Controller = new PlayerController();
            Actor owner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "owner", false, false, 0);
            world.Map.PlaceActorAt(player, new Point(1, 1));
            world.Map.PlaceActorAt(owner, new Point(5, 1));
            world.SetPlayer(player);
            XpdBase claim = new XpdBase(owner, new[] { new Point(1, 1) });
            world.Map.AddXpdBase(claim);
            Check.Equal(false, claim.Owns(player), "player is outsider to NPC faction base");
            Check.Equal(false, world.Game.Rules.AreEnemies(player, owner),
                "same-faction thief starts neutral");

            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.WALL_BRICK);
            ItemFood unseen = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(unseen, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game,
                new Point(1, 1), unseen)), "unseen theft is legal");
            Check.Equal(false, world.Game.Rules.AreEnemies(player, owner),
                "owner behind wall does not identify thief");

            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.FLOOR_ASPHALT);
            owner.IsSleeping = true;
            ItemFood sleeping = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(sleeping, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game,
                new Point(1, 1), sleeping)), "theft is possible beside sleeping owner");
            Check.Equal(false, world.Game.Rules.AreEnemies(player, owner),
                "sleeping owner does not witness theft");
            owner.IsSleeping = false;
            ItemFood witnessed = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(witnessed, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game,
                new Point(1, 1), witnessed)), "visible theft remains legal");
            Check.Equal(true, world.Game.Rules.AreEnemies(player, owner),
                "witnessed theft makes owner hostile to thief");
        });
    }
}
