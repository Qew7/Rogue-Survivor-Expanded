using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class FollowerOtherMapScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/follower-other-map", () => TownScenarioFactory.Arena(7998,
            ".....", ".....", "....."), world =>
        {
            Map basement = NewMap(world, 7999, "basement");
            Map destination = NewMap(world, 8000, "destination");
            world.Map.District.AddUniqueMap(basement);
            world.Map.District.AddUniqueMap(destination);
            Actor leader = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(leader, new Point(1, 1));
            Actor follower = SkillScenario.Actor(world);
            world.Map.RemoveActor(follower);
            basement.PlaceActorAt(follower, new Point(2, 1));
            leader.AddFollower(follower);
            world.Map.SetExitAt(new Point(1, 1), new Exit(destination, new Point(1, 1)));
            Check.Equal(true, world.Game.DoLeaveMap(leader, new Point(1, 1), false),
                "leader crosses to destination");
            Check.Same(basement, follower.Location.Map, "follower in another map cannot follow");
            Check.Equal(true, basement.HasActor(follower), "follower stays listed in original map");
            Check.Equal(false, destination.HasActor(follower), "destination does not duplicate follower");

            // Reproduce the old save shape: one actor listed on two maps, with one true location.
            destination.PlaceActorAt(follower, new Point(2, 1));
            string path = Path.Combine(Path.GetTempPath(), "follower-other-map-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map oldMap = null, actualMap = null;
                foreach (Map map in loaded.World[0, 0].Maps)
                {
                    if (map.Name == "basement") oldMap = map;
                    if (map.Name == "destination") actualMap = map;
                    map.ReconstructAuxiliaryFields();
                }
                Check.Equal(true, oldMap != null && actualMap != null, "both maps survive save/load");
                Check.Equal(false, oldMap.HasActor(actualMap.GetActorAt(2, 1)),
                    "stale map membership is removed on load");
                Check.Equal(true, actualMap.GetActorAt(2, 1) != null,
                    "follower remains on its actual map");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static Map NewMap(ScenarioWorld world, int seed, string name)
    {
        Map map = new Map(seed, name, 5, 3);
        for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
            map.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
        return map;
    }
}
