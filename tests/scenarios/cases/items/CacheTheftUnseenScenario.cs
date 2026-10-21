using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheTheftUnseenScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/cache-theft-unseen", () => TownScenarioFactory.Arena(4761,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            NpcIntentSupport.Player(world, 8, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            Point at = new Point(2, 1);
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, owner, 1);
            world.Map.PlaceActorAt(owner, at);
            world.Try(new ActionDropItem(owner, world.Game, food));
            world.Map.PlaceActorAt(owner, new Point(6, 1));
            world.SetTile(4, 1, '#');
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "unseen thief takes the owner's food");
            Check.Equal(true, food.IsStolen, "physical provenance survives an unseen theft");
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "distant owner does not learn an invisible thief's identity");
            Check.Equal(true, thief.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "the actual participant knows what they did");
        });
    }
}
