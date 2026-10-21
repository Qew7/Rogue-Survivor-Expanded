using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheInsideLaterBaseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/cache-inside-later-base", () => TownScenarioFactory.Arena(4766,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor andrey = NpcIntentSupport.Actor(world, "Andrey", 1, 1);
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 3, 1);
            NpcIntentSupport.Player(world, 7, 2);
            Point at = new Point(2, 1);
            Point otherAt = new Point(2, 2);
            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 80, 3);
            StorageContainer otherFridge = new StorageContainer("fridge", "closed", "open", 80, 3);
            world.Map.PlaceMapObjectAt(fridge, at);
            world.Map.PlaceMapObjectAt(otherFridge, otherAt);
            world.Try(new ActionUseStorageContainer(petr, world.Game, fridge, true));
            world.Try(new ActionUseStorageContainer(petr, world.Game, otherFridge, true));
            ItemFood first = NpcIntentSupport.Food(world, petr, 1);
            world.Map.PlaceActorAt(petr, at);
            world.Try(new ActionDropItem(petr, world.Game, first));
            ItemFood second = NpcIntentSupport.Food(world, petr, 1);
            world.Map.PlaceActorAt(petr, otherAt);
            world.Try(new ActionDropItem(petr, world.Game, second));
            world.Map.PlaceActorAt(petr, new Point(3, 1));
            andrey.AddFollower(petr);
            world.Map.AddXpdBase(new XpdBase(andrey, new[] { at, otherAt }));
            Check.Equal(true, world.Try(new ActionGetFromContainer(petr, world.Game, at)),
                "member retrieves food stored before the base claim");
            Check.Equal(false, NpcIntentSupport.HasEvent(andrey, "base_theft"),
                "group membership allows retrieval without base theft");
            Check.Equal(false, NpcIntentSupport.HasEvent(andrey, "cache_theft"),
                "retrieving one's own stack is not personal theft");

            petr.Leader.RemoveFollower(petr);
            Check.Equal(true, world.Try(new ActionGetFromContainer(petr, world.Game, otherAt)),
                "outsider retrieves their own food from the claimed building");
            Check.Equal(false, NpcIntentSupport.HasEvent(andrey, "base_theft"),
                "territorial claim does not confiscate the outsider's own preexisting stack");
            Check.Equal(false, NpcIntentSupport.HasEvent(andrey, "cache_theft"),
                "no separate personal theft is emitted");
            Check.Equal(petr.PersonalityIdentity, second.StorageClaimantId,
                "base claim does not rewrite the stack's personal claimant");
        });
    }
}
