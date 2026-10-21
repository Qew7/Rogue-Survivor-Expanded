using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class UnknownCacheInspectionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/inspects-unknown-cache", () => TownScenarioFactory.Arena(4767,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor explorer = NpcIntentSupport.Actor(world, "explorer", 1, 1, "curious");
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            StorageContainer distant = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(distant, new Point(5, 1));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, explorer,
                sensor.Sense(world.Game, explorer), sensor.FOV);
            Check.Equal(-1, explorer.Personality.Knowledge.Caches.First(c => c.Id == cache.Id).Units,
                "unopened contents are unknown");
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, explorer))
                Check.Equal(false, candidate.Capability.Id == "inspect_unknown_cache",
                    "unknown container does not create a lasting goal");
            for (int i = 0; i < 14 && explorer.Personality.Knowledge.Caches.First(c => c.Id == cache.Id).Units < 0; i++)
            {
                explorer.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(explorer);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, cache.IsOpen, "explorer opens an unknown container");
            Check.Equal(1, explorer.Personality.Knowledge.Caches.First(c => c.Id == cache.Id).Units,
                "explorer learns contents by a real inspection");
            Check.Equal(false, distant.IsOpen, "distant container stays outside the local action range");
        });
    }
}
