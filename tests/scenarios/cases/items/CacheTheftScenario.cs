using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class CacheTheftScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/cache-theft", () => TownScenarioFactory.Arena(4757,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            NpcIntentSupport.Player(world, 6, 2);
            var cache = new djack.RogueSurvivor.Engine.MapObjects.StorageContainer("fridge", "closed", "open", 80, 3);
            Point at = new Point(2, 1);
            world.Map.PlaceMapObjectAt(cache, at);
            Check.Equal(true, world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true)),
                "owner opens the fridge");
            ItemFood food = NpcIntentSupport.Food(world, owner, 2);
            world.Map.PlaceActorAt(owner, at);
            Check.Equal(true, world.Try(new ActionDropItem(owner, world.Game, food)),
                "owner deliberately stores food");
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            Check.Equal(owner.PersonalityIdentity, food.StorageClaimantId,
                "stored stack has a personal claimant");
            thief.Inventory.MaxCapacity = 0;
            Check.Equal(false, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "full inventory prevents a pickup");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "cache_theft"),
                "failed transfer reports no theft");
            thief.Inventory.MaxCapacity = 10;
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "thief physically takes the stack");
            Check.Equal(true, thief.Inventory.Contains(food), "taken food reaches inventory");
            Check.Equal(true, food.IsStolen, "taken stack keeps stolen provenance");
            Check.Equal(owner.PersonalityIdentity, food.StolenFromLeaderId,
                "stolen marker points to the original claimant");
            NpcFact theft = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "base_theft");
            Check.Equal(true, theft != null && theft.Units == 2,
                "owner witnesses the actual number taken");
            Check.Equal(owner.PersonalityIdentity, theft.ClaimantId,
                "base theft retains the personal claimant context");
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "personal and base claims produce one theft outcome");
            Check.Equal(false, world.Game.Rules.AreEnemies(thief, owner),
                "witnessed cache theft does not force combat hostility");
        });
    }
}
