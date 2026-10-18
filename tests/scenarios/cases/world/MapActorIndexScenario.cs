using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class MapActorIndexScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/map-actor-index", () => TownScenarioFactory.Arena(7981,
            "....", "....", "...."), world =>
        {
            Actor mover = SkillScenario.Actor(world);
            Actor blocker = SkillScenario.Actor(world);
            world.Place(mover, 1, 1);
            world.Place(blocker, 3, 1);
            world.Map.PlaceActorAt(mover, new Point(2, 1));
            Check.Equal(null, world.Map.GetActorAt(1, 1), "move clears old cell");
            Check.Same(mover, world.Map.GetActorAt(2, 1), "move fills new cell");
            Check.Equal(null, world.Map.GetActorAt(-1, 1), "outside lookup is empty");
            Check.Equal(null, world.Map.GetActorAt(4, 1), "right edge lookup is empty");
            bool blocked = false;
            try { world.Map.PlaceActorAt(mover, new Point(3, 1)); }
            catch (InvalidOperationException) { blocked = true; }
            Check.Equal(true, blocked, "occupied destination rejects move");
            Check.Same(mover, world.Map.GetActorAt(2, 1), "rejected move keeps old cell");
            world.Map.RemoveActor(blocker);
            Check.Equal(null, world.Map.GetActorAt(3, 1), "removal clears cell");
            Map destination = new Map(7984, "neighbor", 4, 3);
            for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++)
                destination.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            world.Map.RemoveActor(mover);
            destination.PlaceActorAt(mover, new Point(0, 0));
            Check.Equal(null, world.Map.GetActorAt(2, 1), "cross-map departure clears origin");
            Check.Same(mover, destination.GetActorAt(0, 0), "cross-map arrival fills destination");
            destination.RemoveActor(mover);
            world.Map.PlaceActorAt(mover, new Point(2, 1));

            string path = Path.Combine(Path.GetTempPath(), "actor-index-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                loaded.World[0, 0].EntryMap.ReconstructAuxiliaryFields();
                Check.Equal(true, loaded.World[0, 0].EntryMap.GetActorAt(2, 1) != null,
                    "load reconstructs occupied cell");
                Check.Equal(null, loaded.World[0, 0].EntryMap.GetActorAt(1, 1),
                    "load keeps old cell empty");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
