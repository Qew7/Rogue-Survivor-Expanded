using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheVisibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-visibility", () => TownScenarioFactory.Arena(4747,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor observer = NpcIntentSupport.Actor(world, "observer", 1, 1);
            StorageContainer cache = new StorageContainer("fridge", "closed", "open", 80, 3);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            cache.Contents.AddAll(food);
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            See(world, observer, sensor);
            Check.Equal(1, observer.Personality.Knowledge.Caches.Count,
                "observer remembers visible closed furniture");
            Check.Equal(-1, observer.Personality.Knowledge.Caches[0].Units,
                "closed furniture does not reveal its contents");
            Check.Equal(true, world.Try(new ActionUseStorageContainer(observer, world.Game, cache, true)),
                "observer opens the container");
            See(world, observer, sensor);
            Check.Equal(1, observer.Personality.Knowledge.Caches.Count,
                "visible open supplies enter bounded knowledge");
            NpcKnownCache known = observer.Personality.Knowledge.Caches[0];
            Check.Equal(1, known.Units, "seen quantity is recorded");
            Check.Equal(NpcKnowledgeSource.Witness, known.Source, "source is direct sight");
            int revision = observer.Personality.Knowledge.Revision;
            world.Map.LocalTime.TurnCounter++;
            See(world, observer, sensor);
            Check.Equal(revision, observer.Personality.Knowledge.Revision,
                "unchanged sight does not trigger a new planning revision");

            StorageContainer empty = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(empty, new Point(1, 2));
            empty.SetState(StorageContainer.OPEN);
            See(world, observer, sensor);
            Check.Equal(2, observer.Personality.Knowledge.Caches.Count,
                "empty open furniture can be known without evicting supplies");
            cache.Contents.RemoveAllQuantity(food);
            See(world, observer, sensor);
            int cacheIndex = observer.Personality.Knowledge.Caches[0].Id == cache.Id ? 0 : 1;
            Check.Equal(0, observer.Personality.Knowledge.Caches[cacheIndex].Units,
                "visible empty cache updates an existing belief");
            cache.SetState(StorageContainer.CLOSED);
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            See(world, observer, sensor);
            cacheIndex = observer.Personality.Knowledge.Caches[0].Id == cache.Id ? 0 : 1;
            Check.Equal(0, observer.Personality.Knowledge.Caches[cacheIndex].Units,
                "closing furniture hides newly placed supplies");
        });
    }

    static void See(ScenarioWorld world, Actor observer, LOSSensor sensor)
    { NpcKnowledgeSystem.Perceive(world.Game, observer, sensor.Sense(world.Game, observer), sensor.FOV); }
}
