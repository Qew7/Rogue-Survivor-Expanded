using System.Drawing;
using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheEncounterChoiceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-encounter-choice", () => TownScenarioFactory.Arena(4872,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "patient", "lawful");
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1, "kind", "lawful");
            owner.Personality.Opinion(thief.PersonalityIdentity, thief.UnmodifiedName);
            thief.Personality.Opinion(owner.PersonalityIdentity, owner.UnmodifiedName);
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.IsMaterialTransparent = true;
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            world.Map.PlaceActorAt(owner, cache.Location.Position);
            world.Try(new ActionDropItem(owner, world.Game, NpcIntentSupport.Food(world, owner, 1)));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, cache.Location.Position)),
                "physical theft succeeds");
            Check.Equal(false, NpcIntentSupport.HasEvent(owner, "cache_return_requested"),
                "seeing a theft is not an automatic demand");
            Check.Equal(false, NpcIntentSupport.HasEvent(thief, "cache_returned"),
                "theft itself does not return the item");
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(owner, "cache_return_requested"); turn++)
            {
                owner.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(owner);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(owner, "cache_return_requested"),
                "patient owner selects a spoken demand");
            NpcFact theft = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft");
            string handled = "victim:" + theft.EventId + ":" + theft.ItemId.ToString("N") +
                ":" + thief.PersonalityIdentity.ToString("N");
            Check.Equal(true, owner.Personality.Knowledge.HasHandledCacheEncounter(handled),
                "the demand is bounded to the specific encounter");
            string path = Path.Combine(Path.GetTempPath(), "cache-encounter-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, owner.PersonalityIdentity);
                Check.Equal(true, saved.Personality.Knowledge.HasHandledCacheEncounter(handled),
                    "handled encounter survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            for (int turn = 0; turn < 12 && !NpcIntentSupport.HasEvent(thief, "cache_returned"); turn++)
            {
                thief.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(thief);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, NpcIntentSupport.HasEvent(thief, "cache_returned"),
                "lawful thief chooses and performs return");
            Check.Equal(true, NpcIntentSupport.FoodUnits(owner) > 0,
                "returned food physically reaches owner");
            Check.Equal(false, world.Game.Rules.AreEnemies(owner, thief),
                "talk and return do not create automatic combat hostility");
        });
    }
}
