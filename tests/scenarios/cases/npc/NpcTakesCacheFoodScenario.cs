using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcTakesCacheFoodScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/takes-cache-food", () => TownScenarioFactory.Arena(4776,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor seeker = NpcIntentSupport.Actor(world, "seeker", 1, 1);
            Actor blocked = NpcIntentSupport.Actor(world, "blocked", 3, 1);
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.SetState(StorageContainer.OPEN);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            ItemNote paper = new ItemNote(world.Game.GameItems.NOTE);
            cache.Contents.AddAll(food);
            cache.Contents.AddAll(paper);
            blocked.Inventory.MaxCapacity = 0;
            Check.Equal(false, world.Try(new ActionGetFromContainer(blocked, world.Game,
                cache.Location.Position, food)), "full inventory blocks a targeted transfer");
            Check.Equal(true, cache.Contents.Contains(food), "failed transfer leaves food inside");
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, seeker,
                sensor.Sense(world.Game, seeker), sensor.FOV);
            Check.Equal(1, seeker.Personality.Knowledge.Caches[0].Units,
                "seeker sees food but the paper is not counted as supply");
            for (int i = 0; i < 12 && !seeker.Inventory.Contains(food); i++)
            {
                seeker.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(seeker);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, seeker.Inventory.Contains(food),
                "NPC chooses and physically takes the food beneath the paper");
            Check.Equal(true, cache.Contents.Contains(paper), "targeted pickup leaves the paper");
        });
    }
}
