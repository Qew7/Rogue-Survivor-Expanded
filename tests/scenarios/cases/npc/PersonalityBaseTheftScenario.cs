using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class PersonalityBaseTheftScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-base-theft", () => TownScenarioFactory.Arena(4516,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor thief = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "thief", false, false, 0);
            thief.Controller = new PlayerController();
            Actor owner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "owner", false, false, 0);
            owner.Personality = new PersonalityState();
            world.Place(thief, 1, 1);
            world.Place(owner, 5, 1);
            world.SetPlayer(thief);
            XpdBase claim = new XpdBase(owner, new[] { new Point(1, 1) });
            claim.SetFoodRoom(new Rectangle(1, 1, 1, 1));
            world.Map.AddXpdBase(claim);
            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.WALL_BRICK);
            ItemFood unseen = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(unseen, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game,
                new Point(1, 1), unseen)), "hidden theft is legal");
            Check.Equal(0, owner.Personality.Memories.Count,
                "owner cannot remember an unseen theft");

            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.FLOOR_ASPHALT);
            ItemFood witnessed = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(witnessed, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(thief, world.Game,
                new Point(1, 1), witnessed)), "visible theft is legal");
            Check.Equal(2, owner.Personality.Memories.Count,
                "witnessed storage theft creates theft and lost supplies memories");
            Check.Equal(true, world.Game.Rules.AreEnemies(thief, owner),
                "existing theft hostility still applies");
        });
    }
}
