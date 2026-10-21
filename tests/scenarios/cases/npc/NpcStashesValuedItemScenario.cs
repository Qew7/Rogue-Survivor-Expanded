using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcStashesValuedItemScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stashes-valued-item", () => TownScenarioFactory.Arena(4787,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor keeper = NpcIntentSupport.Actor(world, "keeper", 1, 1, "hoarder");
            NpcIntentSupport.Player(world, 6, 2);
            var drawer = new StorageContainer("drawer", "closed", "open", 40, 3);
            drawer.SetState(StorageContainer.OPEN);
            Point at = new Point(2, 1);
            world.Map.PlaceMapObjectAt(drawer, at);
            ItemTracker treasured = new ItemTracker(world.Game.GameItems.CELL_PHONE);
            keeper.Inventory.AddAll(treasured);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, keeper,
                sensor.Sense(world.Game, keeper), sensor.FOV);
            bool unvaluedStash = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "stash_food") unvaluedStash = true;
            Check.Equal(false, unvaluedStash, "ordinary carried item does not become a personal stash goal");
            keeper.Personality.Attach(new NpcAttachment { Kind = "item", ModelId = treasured.Model.ID,
                ItemId = treasured.StoryIdentity, Weight = 35 });
            bool valuedStash = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "stash_food") valuedStash = true;
            Check.Equal(true, valuedStash, "personally valued item can motivate a stash");
            for (int i = 0; i < 16 && !drawer.Contents.Contains(treasured); i++)
            {
                keeper.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(keeper);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, drawer.Contents.Contains(treasured),
                "NPC physically hides the valued phone");
            Check.Equal(keeper.PersonalityIdentity, treasured.StorageClaimantId,
                "the stored valued item remains the keeper's own property");
            NpcKnownCache known = null;
            foreach (NpcKnownCache cache in keeper.Personality.Knowledge.Caches)
                if (cache.Id == drawer.Id) known = cache;
            Check.Equal(treasured.StoryIdentity, known.OwnItemId,
                "keeper remembers which particular possession was stashed");
            NpcKnowledgeSystem.Perceive(world.Game, keeper,
                sensor.Sense(world.Game, keeper), sensor.FOV);
            bool falseRecovery = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "recover_valued_item") falseRecovery = true;
            Check.Equal(false, falseRecovery,
                "the intentional stash is not mistaken for a lost possession");

            string path = Path.Combine(Path.GetTempPath(), "npc-valued-stash-" + Guid.NewGuid().ToString("N"));
            try
            {
                Guid keeperId = keeper.PersonalityIdentity;
                Guid itemId = treasured.StoryIdentity;
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(loaded, keeperId);
                StorageContainer restoredDrawer = loaded.GetMapObjectAt(at) as StorageContainer;
                NpcKnownCache restoredKnowledge = null;
                foreach (NpcKnownCache cache in restored.Personality.Knowledge.Caches)
                    if (cache.Id == restoredDrawer.Id) restoredKnowledge = cache;
                Check.Equal(itemId, restoredKnowledge.OwnItemId,
                    "personal valued-item belief survives save and load");
                bool itemSaved = false;
                foreach (Item item in restoredDrawer.Contents.Items)
                    if (item.StoryIdentity == itemId) itemSaved = true;
                Check.Equal(true, itemSaved, "the physical valued item survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            keeper.FoodPoints = keeper.Sheet.BaseFoodPoints / 4;
            bool retrieveWanted = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, keeper))
                if (candidate.Capability.Id == "take_cache_valued_item") retrieveWanted = true;
            Check.Equal(true, retrieveWanted,
                "hungry keeper considers retrieving the prized item to carry or trade");
            for (int i = 0; i < 24 && !keeper.Inventory.Contains(treasured); i++)
            {
                keeper.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(keeper);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, keeper.Inventory.Contains(treasured),
                "NPC can later return and physically retrieve the same valued item");
            Check.Equal(false, drawer.Contents.Contains(treasured),
                "retrieval removes the item from the furniture inventory");
        });
    }
}
