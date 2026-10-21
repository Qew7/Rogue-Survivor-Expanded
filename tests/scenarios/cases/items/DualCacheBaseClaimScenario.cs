using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class DualCacheBaseClaimScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/cache-and-base-claim", () => TownScenarioFactory.Arena(4769,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor andrey = NpcIntentSupport.Actor(world, "Andrey", 1, 1);
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 3, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 4, 1);
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            StorageContainer cache = new StorageContainer("fridge", "closed", "open", 80, 3);
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(petr, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, petr, 2);
            world.Map.PlaceActorAt(petr, at);
            world.Try(new ActionDropItem(petr, world.Game, food));
            world.Map.PlaceActorAt(petr, new Point(1, 2));
            world.Map.AddXpdBase(new XpdBase(andrey, new[] { at }));
            Check.Equal(petr.PersonalityIdentity, food.StorageClaimantId,
                "Petr's claim exists before the base pickup");
            thief.Personality.Permit(new NpcSupplyPermission { Grantor = petr.PersonalityIdentity,
                Place = cache.Location, Resource = "food", Units = 2, ExpiresTurn = 100, CauseId = 71 });
            thief.Personality.Permit(new NpcSupplyPermission { Grantor = andrey.PersonalityIdentity,
                Place = cache.Location, Resource = "food", Units = 1, ExpiresTurn = 100, CauseId = 72 });
            world.Map.PlaceActorAt(thief, new Point(3, 2));
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "third actor takes a stack with two independent claimants");
            int clean = 0, stolen = 0;
            foreach (Item item in thief.Inventory.Items)
                if (item is ItemFood)
                { if (item.IsStolen) stolen += item.Quantity; else clean += item.Quantity; }
            Check.Equal(1, clean, "only the shared permitted quantity is lawful");
            Check.Equal(1, stolen, "base permission does not cover the second unit");
            NpcFact theft = andrey.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(true, theft != null, "one territorial theft event is visible");
            Check.Equal(petr.PersonalityIdentity, food.StorageClaimantId,
                "Petr's claim remains on the transferred source item");
            NpcFact thiefFact = thief.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(petr.PersonalityIdentity, thiefFact.ClaimantId,
                "participant receives the separate claimant context");
            Check.Equal(1, theft.Units, "event counts the stolen unit");
            Check.Equal(andrey.PersonalityIdentity, theft.OtherId,
                "the base leader remains the territorial victim");
            Check.Equal(petr.PersonalityIdentity, theft.ClaimantId,
                "event retains the separate personal claimant");
            Check.Equal(false, andrey.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "the same pickup does not produce a second cache theft");
            Check.Equal(1, thief.Personality.Permissions.Count,
                "unused personal permission remains after the partial transfer");
        });
    }
}
