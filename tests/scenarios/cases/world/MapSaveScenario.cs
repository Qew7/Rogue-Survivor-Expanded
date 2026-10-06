using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class MapSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/map-roundtrip", () => TownScenarioFactory.Arena(4306,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(2, 1));
            world.Map.SetTileModelAt(3, 1, world.Game.GameTiles.WALL_BRICK);
            world.Map.AddZone(new Zone("saved room", new Rectangle(1, 0, 2, 2)));
            world.Map.GetZonesAt(1, 0); // Build the transient zone index before saving.
            string path = Path.Combine(Path.GetTempPath(), "rogue-scenario-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal(true, loaded.GetActorAt(2, 1) != null, "actor survives reload");
                Check.Equal(false, loaded.GetTileAt(3, 1).Model.IsWalkable, "wall survives reload");
                Check.Equal("saved room", loaded.GetZonesAt(1, 0)[0].Name,
                    "zone lookup rebuilds after reload");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
