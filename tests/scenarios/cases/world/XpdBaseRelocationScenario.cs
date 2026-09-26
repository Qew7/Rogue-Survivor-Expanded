using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class XpdBaseRelocationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-relocation", () => TownScenarioFactory.Arena(4412,
            ".......", ".#####.", ".#...#.", ".#...#.", ".#...#.", ".#####.", "......."), world =>
        {
            Session.Get.GameMode = GameMode.GM_XPD;
            District firstDistrict = Session.Get.World[0, 0];
            Map first = world.Map;
            MakeRoom(first);
            Map second = new Map(4413, "second", 7, 7);
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    second.SetTileModelAt(x, y, x == 0 || x == 6 || y == 0 || y == 6 ?
                        world.Game.GameTiles.FLOOR_ASPHALT :
                        x == 1 || x == 5 || y == 1 || y == 5 ?
                        world.Game.GameTiles.WALL_BRICK : world.Game.GameTiles.FLOOR_ASPHALT);
            MakeRoom(second);
            District secondDistrict = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
            secondDistrict.EntryMap = second;
            World expanded = new World(2);
            expanded[0, 0] = firstDistrict;
            expanded[1, 0] = secondDistrict;
            Session.Get.World = expanded;

            Actor player = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "player", false, false, 0);
            player.Controller = new PlayerController();
            first.PlaceActorAt(player, new Point(3, 3));
            world.SetPlayer(player);
            string reason;
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason), "first base claimed");
            XpdBase oldBase = first.XpdBaseAt(new Point(3, 3));
            Check.Equal(true, oldBase != null, "first base stored");

            first.RemoveActor(player);
            second.PlaceActorAt(player, new Point(0, 0));
            Session.Get.CurrentMap = second;
            Check.Equal(false, world.Game.TryClaimXpdBase(player, out reason),
                "claim outside a building fails");
            Check.Equal(true, first.XpdBaseAt(new Point(3, 3)) == oldBase,
                "failed claim retains the first base");
            second.PlaceActorAt(player, new Point(3, 3));
            Check.Equal(true, world.Game.TryClaimXpdBase(player, out reason), "second base claimed");
            Check.Equal(null, first.XpdBaseAt(new Point(3, 3)), "old territory is free");
            Check.Equal(true, second.XpdBaseAt(new Point(3, 3)).Owns(player),
                "new base belongs to player");

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
                world.Game.DrawMiniMap(second);
                Check.Equal(Color.LimeGreen, ui.MinimapColors[new Point(2, 2)],
                    "owned base boundary is highlighted");
                Check.Equal(false, ui.MinimapColors.ContainsKey(new Point(3, 3)),
                    "base interior is not highlighted when unexplored");
                world.Game.DrawMiniMap(first);
                Check.Equal(false, ui.MinimapColors.ContainsKey(new Point(2, 2)),
                    "former base is no longer highlighted");
                world.Game.DrawActorStatus(player, 0, 0);
                Check.Equal(true, ui.DrawnStrings.Contains("(YOU) player, civilian [Base B0]"),
                    "status shows base district beside name and faction");
            }
            finally
            {
                typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, before);
            }

            string path = Path.Combine(Path.GetTempPath(), "base-relocation-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(null, loaded.World[0, 0].EntryMap.XpdBaseAt(new Point(3, 3)),
                    "old base remains free after loading");
                Check.Equal(true, loaded.World[1, 0].EntryMap.XpdBaseAt(new Point(3, 3)) != null,
                    "new base survives loading");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static void MakeRoom(Map map)
    {
        for (int y = 2; y <= 4; y++)
            for (int x = 2; x <= 4; x++) map.GetTileAt(x, y).IsInside = true;
    }
}
