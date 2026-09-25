using System.Drawing;
using djack.RogueSurvivor.Data;

static class ZoneLookupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/zone-lookup", () => TownScenarioFactory.Arena(4581,
            "....", "....", "....", "...."), world =>
        {
            Zone first = new Zone("room one", new Rectangle(1, 1, 2, 2));
            Zone second = new Zone("room two", new Rectangle(2, 2, 2, 2));
            world.Map.AddZone(first);
            world.Map.AddZone(second);
            Check.Equal(null, world.Map.GetZonesAt(0, 0), "outside all zones");
            Check.Equal(2, world.Map.GetZonesAt(2, 2).Count, "overlap contains both zones");
            Check.Equal(first, world.Map.GetZonesAt(1, 1)[0], "zone order is preserved");
            Check.Equal(true, world.Map.HasZonePartiallyNamedAt(new Point(2, 2), "two"),
                "name query finds overlapping zone");
            world.Map.RemoveZone(first);
            Check.Equal(second, world.Map.GetZonesAt(2, 2)[0], "removal updates lookup");
            second.Bounds = new Rectangle(0, 0, 1, 1);
            Check.Equal(null, world.Map.GetZonesAt(2, 2), "changed bounds update lookup");
        });
    }
}
