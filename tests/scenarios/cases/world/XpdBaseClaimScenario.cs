using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;

static class XpdBaseClaimScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-claim", () => TownScenarioFactory.Arena(4401,
            ".............", ".#####.#####.", ".#...#.#...#.",
            ".#...#.#...#.", ".#####.#####.", "............."), world =>
        {
            Map map = world.Map;
            Actor claimant = SkillScenario.Actor(world);
            map.PlaceActorAt(claimant, new Point(3, 2));
            foreach (int x in new[] { 2, 3, 4, 8, 9, 10 })
                foreach (int y in new[] { 2, 3 }) map.GetTileAt(x, y).IsInside = true;
            foreach (int x in new[] { 5, 7 })
            {
                map.SetTileModelAt(x, 2, world.Game.GameTiles.FLOOR_ASPHALT);
                map.PlaceMapObjectAt(new DoorWindow("door", "closed", "open", "broken", 40), new Point(x, 2));
            }
            string reason;
            List<Point> first = XpdBasePlanner.Preview(map, claimant, world.Game.Rules, out reason);
            Check.Equal(true, first.Contains(new Point(3, 2)), "first building can be claimed");
            Check.Equal(false, first.Contains(new Point(9, 2)), "open passage does not claim second building");
            foreach (int y in new[] { 1, 4 })
                map.PlaceMapObjectAt(new Fortification("wall", "wall", 40), new Point(6, y));
            List<Point> joined = XpdBasePlanner.Preview(map, claimant, world.Game.Rules, out reason);
            Check.Equal(true, joined.Contains(new Point(6, 2)), "enclosed barricade corridor joins the base");
            Check.Equal(true, joined.Contains(new Point(9, 2)), "joined building is included");
            Actor zombie = SkillScenario.Actor(world, true);
            map.PlaceActorAt(zombie, new Point(9, 2));
            List<Point> safe = XpdBasePlanner.Preview(map, claimant, world.Game.Rules, out reason);
            Check.Equal(true, safe.Contains(new Point(3, 2)), "safe building remains claimable");
            Check.Equal(false, safe.Contains(new Point(9, 2)), "undead building is excluded");
            map.PlaceActorAt(zombie, new Point(0, 0));
            map.PlaceActorAt(zombie, new Point(4, 2));
            Check.Equal(null, XpdBasePlanner.Preview(map, claimant, world.Game.Rules, out reason),
                "undead in claimant's room prevents occupation");
            map.PlaceActorAt(zombie, new Point(0, 0));
            joined = XpdBasePlanner.Preview(map, claimant, world.Game.Rules, out reason);
            XpdBase baseClaim = new XpdBase(claimant, joined);
            baseClaim.SetFoodRoom(XpdBasePlanner.RoomAt(map, baseClaim, new Point(3, 2)));
            map.AddXpdBase(baseClaim);
            Check.Same(baseClaim, map.XpdBaseAt(new Point(9, 2)), "base owns joined building");
            string path = Path.Combine(Path.GetTempPath(), "xpd-base-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal(true, loaded.XpdBaseAt(new Point(9, 2)) != null, "base survives save/load");
                Check.Equal(true, loaded.XpdBaseAt(new Point(3, 2)).FoodRoom != null,
                    "storage assignment survives save/load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
