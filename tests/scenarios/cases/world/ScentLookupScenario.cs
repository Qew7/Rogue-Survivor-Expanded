using System.Drawing;
using djack.RogueSurvivor.Data;

static class ScentLookupScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/scent-lookup", () => TownScenarioFactory.Arena(4582,
            "...", "...", "..."), world =>
        {
            Point tile = new Point(1, 1);
            world.Map.ModifyScentAt(Odor.LIVING, 3, tile);
            world.Map.ModifyScentAt(Odor.LIVING, 2, tile);
            Check.Equal(5, world.Map.GetScentByOdorAt(Odor.LIVING, tile),
                "same odor merges its strength");
            world.Map.RefreshScentAt(Odor.LIVING, 4, tile);
            Check.Equal(5, world.Map.GetScentByOdorAt(Odor.LIVING, tile),
                "weaker refresh does not reduce strength");
            world.Map.RefreshScentAt(Odor.LIVING, 8, tile);
            Check.Equal(8, world.Map.GetScentByOdorAt(Odor.LIVING, tile),
                "stronger refresh updates strength");
            Check.Equal(1, new System.Collections.Generic.List<OdorScent>(world.Map.Scents).Count,
                "one scent per odor and tile");
        });
    }
}
