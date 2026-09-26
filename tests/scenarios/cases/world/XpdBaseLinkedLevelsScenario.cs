using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;

static class XpdBaseLinkedLevelsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-linked-levels", () => TownScenarioFactory.Arena(4424,
            ".......", ".#####.", ".#...#.", ".#...#.", ".#...#.",
            ".#####.", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            Map surface = world.Map;
            MarkRoom(surface);
            Map basement = MakeLevel(world, 4425, "basement");
            Map subway = MakeLevel(world, 4426, "subway");
            District district = surface.District;
            district.AddUniqueMap(basement);
            district.AddUniqueMap(subway);
            surface.SetExitAt(new Point(3, 3), new Exit(basement, new Point(3, 3)));
            basement.SetExitAt(new Point(3, 3), new Exit(surface, new Point(3, 3)));

            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", false, false, 0);
            player.Controller = new PlayerController();
            surface.PlaceActorAt(player, new Point(3, 3));
            world.SetPlayer(player);
            string reason;
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason), "surface claimed");
            XpdBase root = surface.XpdBaseAt(new Point(3, 3));
            surface.RemoveActor(player);
            basement.PlaceActorAt(player, new Point(3, 3));
            Session.Get.CurrentMap = basement;
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason),
                "linked basement joins existing base");
            XpdBase lower = basement.XpdBaseAt(new Point(3, 3));
            Check.Equal(true, lower.IsPartOf(root), "basement and house form one base");
            Check.Same(root, surface.XpdBaseAt(new Point(3, 3)),
                "adding basement retains surface claim");

            // A station room can join through stairs; open tracks remain outside it.
            subway.SetTileModelAt(1, 3, world.Game.GameTiles.FLOOR_ASPHALT);
            subway.SetTileModelAt(2, 3, world.Game.GameTiles.FLOOR_ASPHALT);
            subway.GetTileAt(1, 3).IsInside = false;
            subway.PlaceMapObjectAt(new DoorWindow("door", "closed", "open", "broken", 40),
                new Point(2, 3));
            basement.SetExitAt(new Point(4, 3), new Exit(subway, new Point(3, 3)));
            subway.SetExitAt(new Point(3, 3), new Exit(basement, new Point(4, 3)));
            basement.RemoveActor(player);
            subway.PlaceActorAt(player, new Point(3, 3));
            Session.Get.CurrentMap = subway;
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason),
                "sealed station room joins via stairs");
            XpdBase station = subway.XpdBaseAt(new Point(3, 3));
            Check.Equal(true, station.IsPartOf(root), "station room is part of base");
            Check.Equal(null, subway.XpdBaseAt(new Point(1, 3)),
                "open station tracks remain unclaimed");

            ScenarioUI ui = (ScenarioUI)typeof(RogueGame).GetField("m_UI",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world.Game);
            GameOptions before = RogueGame.Options;
            GameOptions options = before;
            options.IsMinimapOn = true;
            typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, options);
            try
            {
                world.Game.ComputeViewRect(player.Location.Position);
                world.Game.DrawMiniMap(subway);
                Check.Equal(Color.LimeGreen, ui.MinimapColors[new Point(2, 2)],
                    "linked station section has a highlighted boundary");
            }
            finally
            {
                typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, before);
            }

            string path = Path.Combine(Path.GetTempPath(), "base-levels-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                District saved = loaded.World[0, 0];
                XpdBase savedRoot = saved.EntryMap.XpdBaseAt(new Point(3, 3));
                Map savedBasement = null, savedSubway = null;
                foreach (Map map in saved.Maps)
                {
                    if (map.Name == "basement") savedBasement = map;
                    if (map.Name == "subway") savedSubway = map;
                }
                Check.Equal(true, savedBasement.XpdBaseAt(new Point(3, 3)).IsPartOf(savedRoot),
                    "basement link survives loading");
                Check.Equal(true, savedSubway.XpdBaseAt(new Point(3, 3)).IsPartOf(savedRoot),
                    "station link survives loading");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }

            Map detached = MakeLevel(world, 4427, "detached");
            district.AddUniqueMap(detached);
            subway.RemoveActor(player);
            detached.PlaceActorAt(player, new Point(3, 3));
            Session.Get.CurrentMap = detached;
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason),
                "unlinked room becomes a replacement base");
            Check.Equal(null, surface.XpdBaseAt(new Point(3, 3)),
                "replacement releases surface section");
            Check.Equal(null, basement.XpdBaseAt(new Point(3, 3)),
                "replacement releases basement section");
            Check.Equal(null, subway.XpdBaseAt(new Point(3, 3)),
                "replacement releases station section");
        });
    }

    static Map MakeLevel(ScenarioWorld world, int seed, string name)
    {
        Map map = new Map(seed, name, 7, 7);
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                map.SetTileModelAt(x, y, x == 0 || x == 6 || y == 0 || y == 6 ?
                    world.Game.GameTiles.FLOOR_ASPHALT :
                    x == 1 || x == 5 || y == 1 || y == 5 ?
                    world.Game.GameTiles.WALL_BRICK : world.Game.GameTiles.FLOOR_ASPHALT);
        MarkRoom(map);
        return map;
    }

    static void MarkRoom(Map map)
    {
        for (int y = 2; y <= 4; y++)
            for (int x = 2; x <= 4; x++) map.GetTileAt(x, y).IsInside = true;
    }
}
