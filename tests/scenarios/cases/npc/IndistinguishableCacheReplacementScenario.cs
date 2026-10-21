using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class IndistinguishableCacheReplacementScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/indistinguishable-cache-replacement", () => TownScenarioFactory.Arena(4771,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 1, 1);
            Actor andrey = NpcIntentSupport.Actor(world, "Andrey", 3, 1);
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 80, 3);
            world.Map.PlaceMapObjectAt(fridge, at);
            world.Try(new ActionUseStorageContainer(petr, world.Game, fridge, true));
            ItemFood original = NpcIntentSupport.Food(world, petr, 1);
            world.Map.PlaceActorAt(petr, at);
            world.Try(new ActionDropItem(petr, world.Game, original));
            world.Map.PlaceActorAt(petr, new Point(6, 1));
            world.SetTile(4, 1, '#');
            Check.Equal(true, world.Try(new ActionGetFromContainer(andrey, world.Game, at)),
                "Andrey takes Petr's food unseen");
            world.Map.PlaceActorAt(andrey, at);
            ItemFood replacement = NpcIntentSupport.Food(world, andrey, 1);
            Check.Equal(true, world.Try(new ActionDropItem(andrey, world.Game, replacement)),
                "Andrey replaces it with the same visible food");
            world.Map.PlaceActorAt(andrey, new Point(7, 1));
            world.Map.PlaceActorAt(petr, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionInspectStorage(petr, world.Game, fridge)),
                "Petr inspects visually unchanged contents");
            Check.Equal(false, petr.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_loss_discovered"),
                "hidden claimant metadata is not evidence of a loss");
            Check.Equal(1, petr.Personality.Knowledge.Caches[0].OwnUnits,
                "Petr retains his expectation without identifying the replacement");
            Check.Equal(true, andrey.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "the original physical theft remains a separate fact");
        });
    }
}
