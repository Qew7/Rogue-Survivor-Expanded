using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Generators;

static class StorageBumpScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/storage-player-bump", () => TownScenarioFactory.Arena(4792,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, BaseTownGenerator.DEFAULT_PARAMS);
            string[] images = { GameImages.OBJ_WARDROBE, GameImages.OBJ_HOSPITAL_WARDROBE,
                GameImages.OBJ_NIGHT_TABLE, GameImages.OBJ_HOSPITAL_NIGHT_TABLE };
            for (int i = 0; i < images.Length; i++)
            {
                string method = i < 2 ? "MakeObjWardrobe" : "MakeObjNightTable";
                StorageContainer storage = (StorageContainer)Check.CallOn(typeof(BaseMapGenerator), generator,
                    method, images[i]);
                storage.SetState(StorageContainer.CLOSED);
                world.Map.PlaceMapObjectAt(storage, new Point(2, 1));
                ActionBump bump = new ActionBump(player, world.Game, Direction.E);
                Check.Equal(true, bump.ConcreteAction is ActionUseStorageContainer,
                    "bump chooses the common opening action for " + images[i] + " (got " +
                    (bump.ConcreteAction == null ? "null" : bump.ConcreteAction.GetType().Name) + ")");
                Check.Equal(true, world.Try(bump), "player opens furniture by bumping");
                Check.Equal(true, storage.IsOpen, "bump opens the container");
                Check.Equal(images[i] + "_open", storage.ImageID, "bump selects the open sprite");
                world.Game.RedrawPlayScreen();
                world.Map.RemoveMapObjectAt(2, 1);
            }
        });
    }
}
