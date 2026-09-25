using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class XpdSupplyRouteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/supply-route", () => TownScenarioFactory.Arena(4405,
            "...", "...", "..."), world =>
        {
            Map home = world.Map;
            District nextDistrict = new District(new Point(1, 0), DistrictKind.RESIDENTIAL);
            District farDistrict = new District(new Point(2, 0), DistrictKind.RESIDENTIAL);
            Map next = MakeMap(world, nextDistrict, 4406);
            Map far = MakeMap(world, farDistrict, 4407);
            Exit toNext = Link(home, next, new Point(2, 1), new Point(0, 1));
            Link(next, home, new Point(0, 1), new Point(2, 1));
            Link(next, far, new Point(2, 1), new Point(0, 1));
            Link(far, next, new Point(0, 1), new Point(2, 1));
            ItemFood distantFood = new ItemFood(world.Game.GameItems.GROCERIES);
            far.DropItemAt(distantFood, new Point(1, 1));
            Location location;
            Item item;
            Check.Equal(false, XpdSupplyRoutes.FindSupply(home, home.District, out location, out item),
                "expedition stays within home and neighboring districts");
            ItemFood nearbyFood = new ItemFood(world.Game.GameItems.GROCERIES);
            next.DropItemAt(nearbyFood, new Point(1, 1));
            Check.Equal(true, XpdSupplyRoutes.FindSupply(home, home.District, out location, out item),
                "neighboring district is searched");
            Check.Same(next, location.Map, "food found across district border");
            Check.Same(nearbyFood, item, "nearby supply selected");
            Check.Same(toNext, XpdSupplyRoutes.NextExit(home, next, home.District),
                "first exit points toward supply");
            Check.Equal(null, XpdSupplyRoutes.NextExit(home, far, home.District),
                "route does not roam beyond neighboring district");
        });
    }

    static Map MakeMap(ScenarioWorld world, District district, int seed)
    {
        Map map = new Map(seed, "neighbor", 3, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                map.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
        district.EntryMap = map;
        return map;
    }

    static Exit Link(Map from, Map to, Point origin, Point target)
    {
        Exit exit = new Exit(to, target) { IsAnAIExit = true };
        from.SetExitAt(origin, exit);
        return exit;
    }
}
