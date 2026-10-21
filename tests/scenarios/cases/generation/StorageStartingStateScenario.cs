using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Generators;

static class StorageStartingStateScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/storage-starting-state", () => TownScenarioFactory.Arena(4765,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, BaseTownGenerator.DEFAULT_PARAMS);
            DiceRoller roller = new DiceRoller(4765);
            bool sawOpen = false, sawClosed = false;
            for (int i = 0; i < 24; i++)
            {
                StorageContainer drawer = (StorageContainer)Check.CallOn(typeof(BaseMapGenerator), generator,
                    "MakeObjDrawer", GameImages.OBJ_DRAWER);
                Check.Equal(true, drawer.IsOpen && drawer.Contents.IsEmpty,
                    "an empty generated drawer starts open");
                StorageContainer fridge = (StorageContainer)Check.CallOn(typeof(BaseMapGenerator), generator,
                    "MakeObjFridge", GameImages.OBJ_FRIDGE);
                Check.Equal(true, fridge.IsOpen && fridge.Contents.IsEmpty,
                    "an empty generated fridge starts open");
                ItemFood food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
                Check.CallOn(typeof(BaseMapGenerator), generator, "AddStartingContainerItem",
                    roller, world.Map, new Point(2, 1), fridge, food);
                Check.Equal(true, fridge.Contents.Contains(food),
                    "generated food belongs to the fridge");
                sawOpen |= fridge.IsOpen;
                sawClosed |= !fridge.IsOpen;
            }
            Check.Equal(true, sawOpen && sawClosed,
                "filled fridges can start in either state under the fixed seed");
        });
    }
}
