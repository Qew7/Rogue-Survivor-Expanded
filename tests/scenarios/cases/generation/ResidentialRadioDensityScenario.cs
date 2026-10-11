using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.Generators;

static class ResidentialRadioDensityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/residential-radio-density", () => TownScenarioFactory.Create(5200, false), world =>
        {
            int wallRadios = 0, portableRadios = 0, sharedHouses = 0;
            for (int seed = 5200; seed < 5240; seed++)
            {
                Session.Get.Seed = seed;
                BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
                parameters.MapWidth = parameters.MapHeight = 40;
                parameters.GeneratePoliceStation = false;
                parameters.GenerateHospital = false;
                parameters.District = new District(new Point(0, 0), DistrictKind.RESIDENTIAL);
                Map map = new BaseTownGenerator(world.Game, parameters).Generate(seed);
                foreach (Zone house in map.Zones.Where(zone => zone.BuildingKind == BuildingKind.House))
                {
                    Point[] walls = map.MapObjects.OfType<RadioReceiver>()
                        .Select(radio => radio.Location.Position).Where(house.Bounds.Contains).ToArray();
                    Point[] portables = map.GroundInventories
                        .Where(items => items.Items.OfType<ItemRadio>().Any())
                        .Select(items => map.GetGroundInventoryPosition(items).Value)
                        .Where(house.Bounds.Contains).ToArray();
                    Check.Equal(true, walls.Length <= 1, "at most one wall radio is generated per house");
                    Check.Equal(true, portables.Length <= 1, "at most one portable radio is generated per house");
                    wallRadios += walls.Length;
                    portableRadios += portables.Length;
                    if (walls.Length == 1 && portables.Length == 1)
                    {
                        sharedHouses++;
                        Check.Equal(false, SharesRoom(map, house.Bounds, walls[0], portables[0]),
                            "wall and portable radios occupy different rooms");
                    }
                }
            }
            Check.Equal(true, wallRadios >= 5 && portableRadios >= 3,
                "fixed residential seeds generate both kinds of radio regularly");
            Check.Equal(true, sharedHouses > 0, "room limit is exercised by houses with both radios");
        });
    }

    static bool SharesRoom(Map map, Rectangle house, Point start, Point target)
    {
        var visited = new HashSet<Point>();
        var pending = new Queue<Point>();
        pending.Enqueue(start);
        visited.Add(start);
        Point[] steps = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };
        while (pending.Count > 0)
        {
            Point current = pending.Dequeue();
            if (current == target) return true;
            foreach (Point step in steps)
            {
                Point next = new Point(current.X + step.X, current.Y + step.Y);
                if (!house.Contains(next) || !visited.Add(next) ||
                    !map.GetTileAt(next).IsInside || !map.GetTileAt(next).Model.IsWalkable ||
                    map.GetMapObjectAt(next) is DoorWindow) continue;
                pending.Enqueue(next);
            }
        }
        return false;
    }
}
