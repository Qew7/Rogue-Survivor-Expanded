using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcTakesCacheMedicineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/takes-cache-medicine", () => TownScenarioFactory.Arena(4782,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor wounded = NpcIntentSupport.Actor(world, "wounded", 1, 1);
            wounded.HitPoints = 1;
            Actor blocked = NpcIntentSupport.Actor(world, "blocked", 3, 1);
            NpcIntentSupport.Player(world, 6, 2);
            var cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            cache.SetState(StorageContainer.OPEN);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            ItemMedicine medicine = new ItemMedicine(world.Game.GameItems.MEDIKIT);
            ItemNote paper = new ItemNote(world.Game.GameItems.NOTE);
            cache.Contents.AddAll(medicine);
            cache.Contents.AddAll(paper);
            blocked.Inventory.MaxCapacity = 0;
            Check.Equal(false, world.Try(new ActionGetFromContainer(blocked, world.Game,
                cache.Location.Position, medicine)), "full inventory blocks medicine transfer");
            Check.Equal(true, cache.Contents.Contains(medicine), "failed transfer leaves medicine inside");
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, wounded,
                sensor.Sense(world.Game, wounded), sensor.FOV);
            bool wantsMedicine = false;
            foreach (NpcGoalCandidate candidate in NpcGoalGenerator.Evaluate(world.Game, wounded))
                if (candidate.Capability.Id == "take_cache_medicine") wantsMedicine = true;
            Check.Equal(true, wantsMedicine, "wounded NPC considers known cache medicine");
            for (int i = 0; i < 16 && cache.Contents.Contains(medicine); i++)
            {
                wounded.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(wounded);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(false, cache.Contents.Contains(medicine),
                "NPC physically retrieves medicine below the paper");
            Check.Equal(true, cache.Contents.Contains(paper), "targeted retrieval leaves the paper");
        });
    }
}
