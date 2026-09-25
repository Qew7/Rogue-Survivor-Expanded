using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class XpdBaseGiftTheftScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-gift-theft", () => TownScenarioFactory.Arena(4430,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "giver", false, false, 0);
            player.Controller = new PlayerController();
            Actor follower = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "recipient", false, false, 0);
            Actor owner = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "witness", false, false, 0);
            world.Map.PlaceActorAt(player, new Point(2, 1));
            world.Map.PlaceActorAt(follower, new Point(2, 2));
            world.Map.PlaceActorAt(owner, new Point(5, 1));
            world.SetPlayer(player);
            player.AddFollower(follower);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { new Point(2, 1) }));
            ItemFood gift = new ItemFood(world.Game.GameItems.GROCERIES);
            Check.Equal(true, player.Inventory.AddAll(gift), "giver carries food");
            world.Game.DoGiveItemTo(player, follower, gift);
            Check.Equal(true, follower.Inventory.Contains(gift), "gift reaches recipient");
            Check.Equal(false, world.Game.Rules.AreEnemies(follower, owner),
                "gift does not accuse recipient of theft");

            ItemFood personal = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            Check.Equal(true, player.Inventory.AddAll(personal), "giver carries personal food");
            world.Game.DoDropItem(player, personal);
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game,
                new Point(2, 1), personal)), "giver retrieves personal food");
            Check.Equal(false, world.Game.Rules.AreEnemies(player, owner),
                "picking up own dropped item is not theft");

            ItemFood stolen = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(stolen, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionTakeItem(player, world.Game,
                new Point(2, 1), stolen)), "actual taking remains possible");
            Check.Equal(true, world.Game.Rules.AreEnemies(player, owner),
                "taking base supplies is still noticed");

            ItemMedicine savedDrop = new ItemMedicine(world.Game.GameItems.MEDIKIT);
            Check.Equal(true, player.Inventory.AddAll(savedDrop), "item for save fits");
            world.Game.DoDropItem(player, savedDrop);
            string path = Path.Combine(Path.GetTempPath(), "gift-theft-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Actor loadedPlayer = null;
                foreach (Actor actor in loaded.Actors)
                    if (actor.Name == player.Name) loadedPlayer = actor;
                Item savedItem = loaded.GetItemsAt(new Point(2, 1)).TopItem;
                Check.Same(loadedPlayer, savedItem.LastDroppedBy,
                    "dropped item provenance survives save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
