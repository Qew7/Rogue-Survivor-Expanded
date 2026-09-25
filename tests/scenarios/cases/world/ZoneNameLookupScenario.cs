using System.Drawing;
using djack.RogueSurvivor.Data;

static class ZoneNameLookupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/zone-name-lookup", () => new ScenarioWorld(7118,
            "....", "...."), world =>
        {
            Zone room = new Zone("patient room", new Rectangle(1, 0, 2, 2));
            Zone hall = new Zone("corridor", new Rectangle(2, 0, 2, 2));
            world.Map.AddZone(room);
            world.Map.AddZone(hall);
            Check.Equal(true, world.Map.HasZonePartiallyNamedAt(new Point(2, 1),
                "patient"), "first overlapping zone matches");
            Check.Equal(true, world.Map.HasZonePartiallyNamedAt(new Point(2, 1),
                "corridor"), "second overlapping zone matches");
            Check.Equal(false, world.Map.HasZonePartiallyNamedAt(new Point(0, 1),
                "patient"), "outside zone does not match");
            Check.Equal(false, world.Map.HasZonePartiallyNamedAt(new Point(1, 1),
                "corridor"), "wrong zone name does not match");
            world.Map.RemoveZone(room);
            Check.Equal(false, world.Map.HasZonePartiallyNamedAt(new Point(2, 1),
                "patient"), "removed zone no longer matches");
        });
    }
}
