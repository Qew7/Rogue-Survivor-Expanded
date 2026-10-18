using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;

static class MapObjectIndexScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/map-object-index", () => TownScenarioFactory.Arena(7975,
            "....", "....", "...."), world =>
        {
            RadioReceiver radio = new RadioReceiver(GameImages.OBJ_RADIO);
            world.Map.PlaceMapObjectAt(radio, new Point(1, 1));
            world.Map.PlaceMapObjectAt(radio, new Point(2, 1));
            Check.Equal(null, world.Map.GetMapObjectAt(1, 1), "moving clears the old cell");
            Check.Same(radio, world.Map.GetMapObjectAt(new Point(2, 1)), "moving indexes the new cell");
            Check.Equal(null, world.Map.GetMapObjectAt(-1, 1), "out-of-bounds lookup stays empty");
            RadioReceiver other = new RadioReceiver(GameImages.OBJ_RADIO);
            world.Map.PlaceMapObjectAt(other, new Point(3, 1));
            bool blocked = false;
            try { world.Map.PlaceMapObjectAt(radio, new Point(3, 1)); }
            catch (InvalidOperationException) { blocked = true; }
            Check.Equal(true, blocked, "occupied destination rejects relocation");
            Check.Same(radio, world.Map.GetMapObjectAt(2, 1), "failed move preserves the index");
            world.Map.RemoveMapObjectAt(3, 1);
            Check.Equal(null, world.Map.GetMapObjectAt(3, 1), "removal clears the index");

            string path = Path.Combine(Path.GetTempPath(), "map-object-index-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                loaded.World[0, 0].EntryMap.ReconstructAuxiliaryFields();
                Check.Equal(true, loaded.World[0, 0].EntryMap.GetMapObjectAt(2, 1) is RadioReceiver,
                    "load reconstructs the map-object index");
                Check.Equal(null, loaded.World[0, 0].EntryMap.GetMapObjectAt(1, 1),
                    "load does not restore an object's old cell");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
