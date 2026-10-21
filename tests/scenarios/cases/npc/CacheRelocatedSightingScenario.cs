using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheRelocatedSightingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-relocated-sighting", () => TownScenarioFactory.Arena(4784,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor observer = NpcIntentSupport.Actor(world, "observer", 1, 1);
            NpcIntentSupport.Player(world, 6, 2);
            var cache = new StorageContainer("fridge", "closed", "open", 80, 3);
            cache.SetState(StorageContainer.OPEN);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, observer,
                sensor.Sense(world.Game, observer), sensor.FOV);
            Check.Equal(1, observer.Personality.Knowledge.Caches[0].Units,
                "open cache contents are initially known");
            cache.SetState(StorageContainer.CLOSED);
            world.Map.PlaceMapObjectAt(cache, new Point(3, 1));
            world.Map.LocalTime.TurnCounter++;
            NpcKnowledgeSystem.Perceive(world.Game, observer,
                sensor.Sense(world.Game, observer), sensor.FOV);
            NpcKnownCache known = observer.Personality.Knowledge.Caches[0];
            Check.Equal(new Point(3, 1), known.Place.Position,
                "seeing the same closed furniture updates its last known position");
            Check.Equal(-1, known.Units,
                "sighting the moved closed fridge does not reveal its contents");
            world.Map.PlaceActorAt(observer, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionUseStorageContainer(observer, world.Game, cache, true)),
                "observer can open the relocated fridge nearby");
            Check.Equal(true, world.Try(new ActionInspectStorage(observer, world.Game, cache)),
                "observer can physically inspect the moved fridge");
            Check.Equal(1, observer.Personality.Knowledge.Caches[0].Units,
                "inspection restores direct knowledge of the contents");
        });
    }
}
