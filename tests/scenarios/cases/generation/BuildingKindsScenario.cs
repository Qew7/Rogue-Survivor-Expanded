using System;
using System.Drawing;
using djack.RogueSurvivor.Data;

static class BuildingKindsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/building-kinds", () => TownScenarioFactory.Create(4833, false), world =>
        {
            int homes = 0;
            foreach (Zone zone in world.Map.Zones)
            {
                if (zone.Name.StartsWith("Housing@", StringComparison.Ordinal))
                { Check.Equal(BuildingKind.House, zone.BuildingKind, "generated houses are typed"); homes++; }
                if (zone.Name.StartsWith("Apartements@", StringComparison.Ordinal))
                { Check.Equal(BuildingKind.Apartments, zone.BuildingKind, "generated apartments are typed"); homes++; }
                if (zone.Name.StartsWith("Grocery@", StringComparison.Ordinal))
                    Check.Equal(BuildingKind.Grocery, zone.BuildingKind, "generated groceries are typed");
                if (zone.Name.StartsWith("Gunshop@", StringComparison.Ordinal))
                    Check.Equal(BuildingKind.Gunshop, zone.BuildingKind, "generated gun shops are typed");
                if (zone.Name.StartsWith("Park@", StringComparison.Ordinal))
                    Check.Equal(BuildingKind.Park, zone.BuildingKind, "generated parks are typed");
            }
            Check.Equal(true, homes > 0, "the seeded town generated a typed residence");
            Check.Equal(null, Zone.BuildingAt(new Location(world.Map, new Point(0, 0))),
                "a street position is not labeled as a building");
        });
    }
}
